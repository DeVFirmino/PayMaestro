using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Gateways;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// What happens when a gateway takes the money and then says nothing. The dangerous move is to
/// assume failure and charge the next acquirer; the correct one is to stop and ask the provider.
/// </summary>
public class ReconciliationTests
{
    [Fact]
    public async Task An_unanswered_charge_stops_the_cascade_instead_of_trying_the_next_acquirer()
    {
        using var db = new PaymentDatabase();
        var silent = TestGateway.Unanswering("Alpha");
        var next = new TestGateway("Beta");

        using var context = db.NewContext();
        var response = await db.NewOrchestrator(context, gateways: [silent, next])
            .Execute("merchant-1", "key-1", PaymentDatabase.Request());

        Assert.Equal(nameof(PaymentStatus.RequiresReconciliation), response.Status);
        Assert.Equal(1, silent.Charges);
        Assert.Equal(0, next.Charges);      // the money may already be gone: never charge again
    }

    [Fact]
    public async Task Reconciling_settles_the_payment_from_the_provider_record_without_charging_again()
    {
        using var db = new PaymentDatabase();
        var silent = TestGateway.Unanswering("Alpha");

        using var chargeContext = db.NewContext();
        var pending = await db.NewOrchestrator(chargeContext, gateways: [silent])
            .Execute("merchant-1", "key-1", PaymentDatabase.Request());

        using var reconcileContext = db.NewContext();
        var settled = await db.NewReconciler(reconcileContext, silent).Execute("merchant-1", pending.Id);

        Assert.Equal(nameof(PaymentStatus.Captured), settled.Status);
        Assert.Equal(1, silent.Charges);    // the query asked, it did not pay
    }

    [Fact]
    public async Task Reconciling_a_key_the_provider_never_recorded_declines_the_payment()
    {
        using var db = new PaymentDatabase();
        var silent = TestGateway.Unanswering("Alpha");

        using var chargeContext = db.NewContext();
        var pending = await db.NewOrchestrator(chargeContext, gateways: [silent])
            .Execute("merchant-1", "key-1", PaymentDatabase.Request());

        // A provider with no memory of the attempt: nothing was charged.
        var forgetful = new TestGateway("Alpha");

        using var reconcileContext = db.NewContext();
        var settled = await db.NewReconciler(reconcileContext, forgetful).Execute("merchant-1", pending.Id);

        Assert.Equal(nameof(PaymentStatus.Declined), settled.Status);
        Assert.Equal(0, forgetful.Charges);
    }

    [Fact]
    public async Task The_attempt_records_the_key_the_provider_was_given()
    {
        using var db = new PaymentDatabase();
        var silent = TestGateway.Unanswering("Alpha");

        using var context = db.NewContext();
        var pending = await db.NewOrchestrator(context, gateways: [silent])
            .Execute("merchant-1", "key-1", PaymentDatabase.Request());

        using var verification = db.NewContext();
        var attempt = await verification.PaymentAttempt.SingleAsync(a => a.PaymentId == pending.Id);

        // Derived, not random: the same attempt always presents the provider the same key.
        Assert.Equal(ProviderIdempotencyKey.For(TestPayment.Reserved(), "Alpha", 1), attempt.ProviderIdempotencyKey);
    }

    [Fact]
    public async Task Should_commit_the_attempt_as_processing_before_the_provider_answers()
    {
        using var db = new PaymentDatabase();
        var reachedGateway = new TaskCompletionSource();
        var releaseGateway = new TaskCompletionSource();

        var gateway = new TestGateway("Alpha", whileCharging: async () =>
        {
            reachedGateway.SetResult();
            await releaseGateway.Task;
        });

        using var context = db.NewContext();
        Task request = db.NewOrchestrator(context, gateways: [gateway])
            .Execute("merchant-1", "key-1", PaymentDatabase.Request());

        await reachedGateway.Task;

        using var observer = db.NewContext();
        var attempt = await observer.PaymentAttempt.SingleAsync();
        Assert.Equal(PaymentAttemptStatus.Processing, attempt.Status);
        Assert.Equal(
            ProviderIdempotencyKey.For(TestPayment.Reserved(), "Alpha", 1), attempt.ProviderIdempotencyKey);

        releaseGateway.SetResult();
        await request;
    }

    [Fact]
    public async Task Should_query_the_provider_key_when_a_processing_attempt_is_stale()
    {
        using var db = new PaymentDatabase();
        var gateway = new TestGateway("Alpha");

        using (var seedContext = db.NewContext())
        {
            var payment = TestPayment.Reserved();

            string providerKey = ProviderIdempotencyKey.For(payment, "Alpha", 1);
            payment.RecordAttempt(PaymentAttempt.Start(payment.Id, "Alpha", 1, providerKey));
            await seedContext.Payment.AddAsync(payment);
            await seedContext.SaveChangesAsync();
            await gateway.ProcessAsync(payment, providerKey);
        }

        using var recoveryContext = db.NewContext();
        int recovered = await db.NewRecovery(recoveryContext, gateway)
            .Execute(DateTime.UtcNow.AddMinutes(1), 10);

        using var verification = db.NewContext();
        var recoveredPayment = await verification.Payment.Include(p => p.Attempts).SingleAsync();

        Assert.Equal(1, recovered);
        Assert.Equal(PaymentStatus.Captured, recoveredPayment.Status);
        Assert.Equal(PaymentAttemptStatus.Completed, recoveredPayment.Attempts.Single().Status);
        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task A_reconciler_working_from_a_stale_payment_loses_to_the_one_that_settled_it()
    {
        using var db = new PaymentDatabase();
        var silent = TestGateway.Unanswering("Alpha");

        using var chargeContext = db.NewContext();
        var pending = await db.NewOrchestrator(chargeContext, gateways: [silent])
            .Execute("merchant-1", "key-1", PaymentDatabase.Request());

        using var firstContext = db.NewContext();
        using var secondContext = db.NewContext();

        // The second reconciler reads the payment while it is still unsettled; by the time it
        // writes, the first has already settled it and the stamp it loaded is stale.
        await secondContext.Payment.Include(p => p.Attempts).FirstAsync(p => p.Id == pending.Id);

        await db.NewReconciler(firstContext, silent).Execute("merchant-1", pending.Id);

        await Assert.ThrowsAsync<ConcurrentPaymentModificationException>(
            () => db.NewReconciler(secondContext, silent).Execute("merchant-1", pending.Id));
    }

    [Fact]
    public async Task Reconciling_a_settled_payment_changes_nothing()
    {
        using var db = new PaymentDatabase();
        var gateway = new TestGateway("Alpha");

        using var chargeContext = db.NewContext();
        var captured = await db.NewOrchestrator(chargeContext, gateways: [gateway])
            .Execute("merchant-1", "key-1", PaymentDatabase.Request());

        using var reconcileContext = db.NewContext();
        var reconciled = await db.NewReconciler(reconcileContext, gateway).Execute("merchant-1", captured.Id);

        Assert.Equal(nameof(PaymentStatus.Captured), reconciled.Status);
        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task Reconciling_an_unknown_payment_is_reported_as_not_found()
    {
        using var db = new PaymentDatabase();

        using var context = db.NewContext();
        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => db.NewReconciler(context, new TestGateway("Alpha")).Execute("merchant-1", Guid.NewGuid()));
    }
}

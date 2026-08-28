using Microsoft.EntityFrameworkCore;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
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
            .Execute("key-1", PaymentDatabase.Request());

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
            .Execute("key-1", PaymentDatabase.Request());

        using var reconcileContext = db.NewContext();
        var settled = await db.NewReconciler(reconcileContext, silent).Execute(pending.Id);

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
            .Execute("key-1", PaymentDatabase.Request());

        // A provider with no memory of the attempt: nothing was charged.
        var forgetful = new TestGateway("Alpha");

        using var reconcileContext = db.NewContext();
        var settled = await db.NewReconciler(reconcileContext, forgetful).Execute(pending.Id);

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
            .Execute("key-1", PaymentDatabase.Request());

        using var verification = db.NewContext();
        var attempt = await verification.PaymentAttempt.SingleAsync(a => a.PaymentId == pending.Id);

        // Derived, not random: the same attempt always presents the provider the same key.
        Assert.Equal("key-1:Alpha:1", attempt.ProviderIdempotencyKey);
    }

    [Fact]
    public async Task A_reconciler_working_from_a_stale_payment_loses_to_the_one_that_settled_it()
    {
        using var db = new PaymentDatabase();
        var silent = TestGateway.Unanswering("Alpha");

        using var chargeContext = db.NewContext();
        var pending = await db.NewOrchestrator(chargeContext, gateways: [silent])
            .Execute("key-1", PaymentDatabase.Request());

        using var firstContext = db.NewContext();
        using var secondContext = db.NewContext();

        // The second reconciler reads the payment while it is still unsettled; by the time it
        // writes, the first has already settled it and the stamp it loaded is stale.
        await secondContext.Payment.Include(p => p.Attempts).FirstAsync(p => p.Id == pending.Id);

        await db.NewReconciler(firstContext, silent).Execute(pending.Id);

        await Assert.ThrowsAsync<ConcurrentPaymentModificationException>(
            () => db.NewReconciler(secondContext, silent).Execute(pending.Id));
    }

    [Fact]
    public async Task Reconciling_a_settled_payment_changes_nothing()
    {
        using var db = new PaymentDatabase();
        var gateway = new TestGateway("Alpha");

        using var chargeContext = db.NewContext();
        var captured = await db.NewOrchestrator(chargeContext, gateways: [gateway])
            .Execute("key-1", PaymentDatabase.Request());

        using var reconcileContext = db.NewContext();
        var reconciled = await db.NewReconciler(reconcileContext, gateway).Execute(captured.Id);

        Assert.Equal(nameof(PaymentStatus.Captured), reconciled.Status);
        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task Reconciling_an_unknown_payment_is_reported_as_not_found()
    {
        using var db = new PaymentDatabase();

        using var context = db.NewContext();
        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => db.NewReconciler(context, new TestGateway("Alpha")).Execute(Guid.NewGuid()));
    }
}

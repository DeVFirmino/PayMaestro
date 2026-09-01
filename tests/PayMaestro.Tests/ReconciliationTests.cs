using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// What happens when a gateway takes the money and then says nothing. The dangerous move is to
/// assume failure and charge the next acquirer; the correct one is to stop and ask the provider.
/// </summary>
public sealed class ReconciliationTests
{
    [Fact]
    public async Task ShouldStopCascadeWhenChargeIsUnanswered()
    {
        using PaymentDatabase db = new();
        TestGateway silent = TestGateway.Unanswering("Alpha");
        TestGateway next = new("Beta");

        using PayMaestroDbContext context = db.NewContext();
        PaymentResponse response = await db.NewCreatePaymentUseCase(context, gateways: [silent, next])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        Assert.Equal(nameof(PaymentStatus.RequiresReconciliation), response.Status);
        Assert.Equal(1, silent.Charges);
        Assert.Equal(0, next.Charges);      // the money may already be gone: never charge again
    }

    [Fact]
    public async Task ShouldSettleFromProviderRecordWhenReconciled()
    {
        using PaymentDatabase db = new();
        TestGateway silent = TestGateway.Unanswering("Alpha");

        using PayMaestroDbContext chargeContext = db.NewContext();
        PaymentResponse pending = await db.NewCreatePaymentUseCase(chargeContext, gateways: [silent])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        using PayMaestroDbContext reconcileContext = db.NewContext();
        PaymentResponse settled = await db.NewReconcilePaymentUseCase(reconcileContext, silent)
            .Execute(pending.Id, CancellationToken.None);

        Assert.Equal(nameof(PaymentStatus.Captured), settled.Status);
        Assert.Equal(1, silent.Charges);    // the query asked, it did not pay
    }

    [Fact]
    public async Task ShouldDeclineWhenProviderHasNoRecordOfKey()
    {
        using PaymentDatabase db = new();
        TestGateway silent = TestGateway.Unanswering("Alpha");

        using PayMaestroDbContext chargeContext = db.NewContext();
        PaymentResponse pending = await db.NewCreatePaymentUseCase(chargeContext, gateways: [silent])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        // A provider with no memory of the attempt: nothing was charged.
        TestGateway forgetful = new("Alpha");

        using PayMaestroDbContext reconcileContext = db.NewContext();
        PaymentResponse settled = await db.NewReconcilePaymentUseCase(reconcileContext, forgetful)
            .Execute(pending.Id, CancellationToken.None);

        Assert.Equal(nameof(PaymentStatus.Declined), settled.Status);
        Assert.Equal(0, forgetful.Charges);
    }

    [Fact]
    public async Task ShouldPersistProviderKeyWhenAttemptIsRecorded()
    {
        using PaymentDatabase db = new();
        TestGateway silent = TestGateway.Unanswering("Alpha");

        using PayMaestroDbContext context = db.NewContext();
        PaymentResponse pending = await db.NewCreatePaymentUseCase(context, gateways: [silent])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        PaymentAttempt attempt = await db.SingleAttemptOfAsync(pending.Id);

        // Derived, not random: the same attempt always presents the provider the same key.
        Assert.Equal("key-1:Alpha:1", attempt.ProviderIdempotencyKey);
    }

    [Fact]
    public async Task ShouldLoseWhenReconcilingFromStalePayment()
    {
        using PaymentDatabase db = new();
        TestGateway silent = TestGateway.Unanswering("Alpha");

        using PayMaestroDbContext chargeContext = db.NewContext();
        PaymentResponse pending = await db.NewCreatePaymentUseCase(chargeContext, gateways: [silent])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        using PayMaestroDbContext firstContext = db.NewContext();
        using PayMaestroDbContext secondContext = db.NewContext();

        // The second reconciler reads the payment while it is still unsettled; by the time it
        // writes, the first has already settled it and the stamp it loaded is stale.
        await PaymentDatabase.LoadIntoAsync(secondContext, pending.Id);

        await db.NewReconcilePaymentUseCase(firstContext, silent).Execute(pending.Id, CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrentPaymentModificationException>(
            () => db.NewReconcilePaymentUseCase(secondContext, silent).Execute(pending.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ShouldChangeNothingWhenReconcilingSettledPayment()
    {
        using PaymentDatabase db = new();
        TestGateway gateway = new("Alpha");

        using PayMaestroDbContext chargeContext = db.NewContext();
        PaymentResponse captured = await db.NewCreatePaymentUseCase(chargeContext, gateways: [gateway])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        using PayMaestroDbContext reconcileContext = db.NewContext();
        PaymentResponse reconciled = await db.NewReconcilePaymentUseCase(reconcileContext, gateway)
            .Execute(captured.Id, CancellationToken.None);

        Assert.Equal(nameof(PaymentStatus.Captured), reconciled.Status);
        Assert.Equal(1, gateway.Charges);
    }

    [Fact]
    public async Task ShouldReportNotFoundWhenReconcilingUnknownPayment()
    {
        using PaymentDatabase db = new();

        using PayMaestroDbContext context = db.NewContext();
        await Assert.ThrowsAsync<PaymentNotFoundException>(
            () => db.NewReconcilePaymentUseCase(context, new TestGateway("Alpha")).Execute(Guid.NewGuid(), CancellationToken.None));
    }
}

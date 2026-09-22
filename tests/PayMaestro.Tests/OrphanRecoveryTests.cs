using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;
using PayMaestro.Infrastructure.Data;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// A request that dies after reserving its key and before saving any attempt leaves a Processing
/// payment nobody can settle: the retry answers 409 and reconciliation has no attempt to ask about.
/// Recovery asks the providers instead, and must never guess and never charge.
/// </summary>
public sealed class OrphanRecoveryTests
{
    private static readonly TimeSpan AnyAge = TimeSpan.Zero;

    [Fact]
    public async Task ShouldSettleFromProviderRecordWhenOrphanWasChargedBeforeRequestDied()
    {
        using PaymentDatabase db = new();
        TestGateway alpha = new("Alpha");
        Payment orphan = await db.SaveOrphanAsync(new PaymentBuilder().BuildReserved());

        // The provider took the money; the request died before saving that it had.
        await alpha.ProcessAsync(orphan, ProviderIdempotencyKey.For(orphan, "Alpha", 1), CancellationToken.None);

        using PayMaestroDbContext context = db.NewContext();
        RecoverOrphanedPaymentsResponse response = await db.NewRecoverOrphanedPaymentsUseCase(context, AnyAge, alpha)
            .Execute(CancellationToken.None);

        Payment recovered = await db.FindCommittedWithAttemptsAsync(orphan.Id);
        Assert.Equal(nameof(PaymentStatus.Captured), Assert.Single(response.Payments).Status);
        Assert.Equal(PaymentStatus.Captured, recovered.Status);
        Assert.Equal("key-1:Alpha:1", Assert.Single(recovered.Attempts).ProviderIdempotencyKey);
        Assert.Equal(1, alpha.Charges);     // recovery asked, it did not pay
    }

    [Fact]
    public async Task ShouldFailWithoutChargeWhenNoProviderHasRecord()
    {
        using PaymentDatabase db = new();
        TestGateway alpha = new("Alpha");
        TestGateway beta = new("Beta");
        Payment orphan = await db.SaveOrphanAsync(new PaymentBuilder().BuildReserved());

        using PayMaestroDbContext context = db.NewContext();
        await db.NewRecoverOrphanedPaymentsUseCase(context, AnyAge, alpha, beta).Execute(CancellationToken.None);

        Payment recovered = await db.FindCommittedWithAttemptsAsync(orphan.Id);
        Assert.Equal(PaymentStatus.FailedWithoutCharge, recovered.Status);
        Assert.Empty(recovered.Attempts);
        Assert.Equal(0, alpha.Charges);
        Assert.Equal(0, beta.Charges);
    }

    [Fact]
    public async Task ShouldFollowCascadeOrderWhenFirstProviderRecordedSoftDecline()
    {
        using PaymentDatabase db = new();
        FixedResultGateway alpha = new("Alpha", GatewayResultType.SoftDecline);
        TestGateway beta = new("Beta");
        Payment orphan = await db.SaveOrphanAsync(new PaymentBuilder().BuildReserved());

        // Alpha declined, so the cascade moved on and Beta took the money as attempt 2.
        await beta.ProcessAsync(orphan, ProviderIdempotencyKey.For(orphan, "Beta", 2), CancellationToken.None);

        using PayMaestroDbContext context = db.NewContext();
        await db.NewRecoverOrphanedPaymentsUseCase(context, AnyAge, alpha, beta).Execute(CancellationToken.None);

        Payment recovered = await db.FindCommittedWithAttemptsAsync(orphan.Id);
        Assert.Equal(PaymentStatus.Captured, recovered.Status);
        Assert.Equal(["Alpha", "Beta"], recovered.Attempts.OrderBy(attempt => attempt.AttemptOrder).Select(attempt => attempt.GatewayName));
        Assert.False(alpha.WasCalled);
        Assert.Equal(1, beta.Charges);
    }

    [Fact]
    public async Task ShouldMarkForReconciliationWhenProviderQueryGivesNoAnswer()
    {
        using PaymentDatabase db = new();
        ThrowingGateway silent = new("Alpha");
        Payment orphan = await db.SaveOrphanAsync(new PaymentBuilder().BuildReserved());

        using PayMaestroDbContext context = db.NewContext();
        await db.NewRecoverOrphanedPaymentsUseCase(context, AnyAge, silent).Execute(CancellationToken.None);

        // Unknown is not "no record": the payment gets an attempt reconcile can ask about later.
        Payment recovered = await db.FindCommittedWithAttemptsAsync(orphan.Id);
        Assert.Equal(PaymentStatus.RequiresReconciliation, recovered.Status);
        Assert.Equal(GatewayResultType.Uncertain, Assert.Single(recovered.Attempts).ResultType);
    }

    [Fact]
    public async Task ShouldLeavePaymentAloneWhenThresholdHasNotPassed()
    {
        using PaymentDatabase db = new();
        TestGateway alpha = new("Alpha");
        Payment orphan = await db.SaveOrphanAsync(new PaymentBuilder().BuildReserved());

        using PayMaestroDbContext context = db.NewContext();
        RecoverOrphanedPaymentsResponse response = await db.NewRecoverOrphanedPaymentsUseCase(context, TimeSpan.FromHours(1), alpha)
            .Execute(CancellationToken.None);

        // Its request may still be charging: settling it now would race the real answer.
        Assert.Empty(response.Payments);
        Assert.Equal(PaymentStatus.Processing, (await db.FindCommittedWithAttemptsAsync(orphan.Id)).Status);
    }

    [Fact]
    public async Task ShouldLeavePaymentAloneWhenItHasRecordedAttempt()
    {
        using PaymentDatabase db = new();
        TestGateway alpha = new("Alpha");
        Payment withAttempt = new PaymentBuilder().BuildReserved();
        withAttempt.RecordAttempt(PaymentAttempt.Create(
            withAttempt.Id, "Alpha", 1, GatewayResultType.SoftDecline, "51", 10, "key-1:Alpha:1"));
        await db.SaveOrphanAsync(withAttempt);

        using PayMaestroDbContext context = db.NewContext();
        RecoverOrphanedPaymentsResponse response = await db.NewRecoverOrphanedPaymentsUseCase(context, AnyAge, alpha)
            .Execute(CancellationToken.None);

        Assert.Empty(response.Payments);
        Assert.Equal(PaymentStatus.Processing, (await db.FindCommittedWithAttemptsAsync(withAttempt.Id)).Status);
    }

    [Fact]
    public async Task ShouldLeaveSettledPaymentAloneWhenRecoveryRuns()
    {
        using PaymentDatabase db = new();
        TestGateway alpha = new("Alpha");

        using PayMaestroDbContext chargeContext = db.NewContext();
        PaymentResponse captured = await db.NewCreatePaymentUseCase(chargeContext, gateways: [alpha])
            .Execute("key-1", new CreatePaymentRequestBuilder().Build(), CancellationToken.None);

        using PayMaestroDbContext context = db.NewContext();
        RecoverOrphanedPaymentsResponse response = await db.NewRecoverOrphanedPaymentsUseCase(context, AnyAge, alpha)
            .Execute(CancellationToken.None);

        Assert.Empty(response.Payments);
        Assert.Equal(1, alpha.Charges);
        Assert.Equal(nameof(PaymentStatus.Captured), captured.Status);
    }
}

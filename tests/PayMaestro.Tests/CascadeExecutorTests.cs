using PayMaestro.Application.UseCases.Payments.CreatePayment;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

public sealed class CascadeExecutorTests
{
    private readonly CascadeExecutor _cascade = new();

    [Fact]
    public async Task ShouldCaptureAndStopWhenFirstGatewayApproves()
    {
        Payment payment = new PaymentBuilder().BuildReserved();
        IReadOnlyList<IPaymentGateway> route = Route(
            new FixedResultGateway("A", GatewayResultType.Approved),
            new FixedResultGateway("B", GatewayResultType.Approved));

        await _cascade.ExecuteAsync(payment, route, CancellationToken.None);

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Single(payment.Attempts);
        Assert.Equal("A", payment.Attempts[0].GatewayName);
    }

    [Fact]
    public async Task ShouldCascadeToNextGatewayWhenFirstSoftDeclines()
    {
        Payment payment = new PaymentBuilder().BuildReserved();
        IReadOnlyList<IPaymentGateway> route = Route(
            new FixedResultGateway("A", GatewayResultType.SoftDecline),
            new FixedResultGateway("B", GatewayResultType.Approved));

        await _cascade.ExecuteAsync(payment, route, CancellationToken.None);

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Equal(2, payment.Attempts.Count);
        Assert.Equal("B", payment.Attempts[1].GatewayName);
    }

    [Fact]
    public async Task ShouldStopImmediatelyWhenGatewayHardDeclines()
    {
        Payment payment = new PaymentBuilder().BuildReserved();
        FixedResultGateway secondGateway = new("B", GatewayResultType.Approved);
        IReadOnlyList<IPaymentGateway> route = Route(
            new FixedResultGateway("A", GatewayResultType.HardDecline),
            secondGateway);

        await _cascade.ExecuteAsync(payment, route, CancellationToken.None);

        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Single(payment.Attempts);
        Assert.False(secondGateway.WasCalled);   // stolen card is never retried elsewhere
    }

    [Fact]
    public async Task ShouldDeclineWhenEveryGatewaySoftDeclinesOrErrors()
    {
        Payment payment = new PaymentBuilder().BuildReserved();
        IReadOnlyList<IPaymentGateway> route = Route(
            new FixedResultGateway("A", GatewayResultType.SoftDecline),
            new FixedResultGateway("B", GatewayResultType.Error));

        await _cascade.ExecuteAsync(payment, route, CancellationToken.None);

        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Equal(2, payment.Attempts.Count);
    }

    [Fact]
    public async Task ShouldWaitForReconciliationWhenOutcomeIsUnknown()
    {
        Payment payment = new PaymentBuilder().BuildReserved();
        FixedResultGateway secondGateway = new("B", GatewayResultType.Approved);
        IReadOnlyList<IPaymentGateway> route = Route(
            new FixedResultGateway("A", GatewayResultType.Uncertain),
            secondGateway);

        await _cascade.ExecuteAsync(payment, route, CancellationToken.None);

        Assert.Equal(PaymentStatus.RequiresReconciliation, payment.Status);
        Assert.False(secondGateway.WasCalled);   // the first acquirer may already hold the money
    }

    [Fact]
    public async Task ShouldRecordUnknownOutcomeWhenGatewayThrows()
    {
        Payment payment = new PaymentBuilder().BuildReserved();
        FixedResultGateway secondGateway = new("B", GatewayResultType.Approved);
        IReadOnlyList<IPaymentGateway> route = Route(new ThrowingGateway("A"), secondGateway);

        await _cascade.ExecuteAsync(payment, route, CancellationToken.None);

        Assert.Equal(PaymentStatus.RequiresReconciliation, payment.Status);
        Assert.Equal(GatewayResultType.Uncertain, payment.Attempts[0].ResultType);
        Assert.False(secondGateway.WasCalled);
    }

    [Fact]
    public async Task ShouldRecordProviderKeyWhenEachGatewayIsAttempted()
    {
        Payment payment = new PaymentBuilder().BuildReserved();
        IReadOnlyList<IPaymentGateway> route = Route(
            new FixedResultGateway("A", GatewayResultType.SoftDecline),
            new FixedResultGateway("B", GatewayResultType.Approved));

        await _cascade.ExecuteAsync(payment, route, CancellationToken.None);

        Assert.Equal("key-1:A:1", payment.Attempts[0].ProviderIdempotencyKey);
        Assert.Equal("key-1:B:2", payment.Attempts[1].ProviderIdempotencyKey);
    }

    private static IReadOnlyList<IPaymentGateway> Route(params IPaymentGateway[] gateways) => [.. gateways];
}

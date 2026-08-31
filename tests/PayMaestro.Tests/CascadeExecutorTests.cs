using PayMaestro.Application.Services;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

public class CascadeExecutorTests
{
    private readonly CascadeExecutor _cascade = new(new NullUnitOfWork());

    [Fact]
    public async Task Approves_on_first_gateway_and_stops()
    {
        var payment = ReservedPayment();
        var route = Route(
            new FakeGateway("A", GatewayResultType.Approved),
            new FakeGateway("B", GatewayResultType.Approved));

        await _cascade.ExecuteAsync(payment, route);

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Single(payment.Attempts);
        Assert.Equal("A", payment.Attempts[0].GatewayName);
    }

    [Fact]
    public async Task Soft_decline_cascades_to_next_gateway()
    {
        var payment = ReservedPayment();
        var route = Route(
            new FakeGateway("A", GatewayResultType.SoftDecline),
            new FakeGateway("B", GatewayResultType.Approved));

        await _cascade.ExecuteAsync(payment, route);

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Equal(2, payment.Attempts.Count);
        Assert.Equal("B", payment.Attempts[1].GatewayName);
    }

    [Fact]
    public async Task Hard_decline_stops_the_cascade_immediately()
    {
        var payment = ReservedPayment();
        var secondGateway = new FakeGateway("B", GatewayResultType.Approved);
        var route = Route(new FakeGateway("A", GatewayResultType.HardDecline), secondGateway);

        await _cascade.ExecuteAsync(payment, route);

        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Single(payment.Attempts);
        Assert.False(secondGateway.WasCalled);   // stolen card is never retried elsewhere
    }

    [Fact]
    public async Task Declines_when_every_gateway_soft_declines()
    {
        var payment = ReservedPayment();
        var route = Route(
            new FakeGateway("A", GatewayResultType.SoftDecline),
            new FakeGateway("B", GatewayResultType.Error));

        await _cascade.ExecuteAsync(payment, route);

        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Equal(2, payment.Attempts.Count);
    }

    [Fact]
    public async Task An_unknown_outcome_stops_the_cascade_and_waits_for_reconciliation()
    {
        var payment = ReservedPayment();
        var secondGateway = new FakeGateway("B", GatewayResultType.Approved);
        var route = Route(new FakeGateway("A", GatewayResultType.Uncertain), secondGateway);

        await _cascade.ExecuteAsync(payment, route);

        Assert.Equal(PaymentStatus.RequiresReconciliation, payment.Status);
        Assert.False(secondGateway.WasCalled);   // the first acquirer may already hold the money
    }

    [Fact]
    public async Task A_gateway_that_throws_is_recorded_as_an_unknown_outcome()
    {
        var payment = ReservedPayment();
        var secondGateway = new FakeGateway("B", GatewayResultType.Approved);
        var route = Route(new ThrowingGateway("A"), secondGateway);

        await _cascade.ExecuteAsync(payment, route);

        Assert.Equal(PaymentStatus.RequiresReconciliation, payment.Status);
        Assert.Equal(GatewayResultType.Uncertain, payment.Attempts[0].ResultType);
        Assert.False(secondGateway.WasCalled);
    }

    [Fact]
    public async Task Each_attempt_carries_the_key_presented_to_its_gateway()
    {
        var payment = ReservedPayment();
        var route = Route(
            new FakeGateway("A", GatewayResultType.SoftDecline),
            new FakeGateway("B", GatewayResultType.Approved));

        await _cascade.ExecuteAsync(payment, route);

        Assert.Equal("merchant-1:key-1:A:1", payment.Attempts[0].ProviderIdempotencyKey);
        Assert.Equal("merchant-1:key-1:B:2", payment.Attempts[1].ProviderIdempotencyKey);
    }

    private static Payment ReservedPayment()
    {
        return TestPayment.Reserved();   // the cascade only ever runs on a reserved payment
    }

    private static List<IPaymentGateway> Route(params IPaymentGateway[] gateways) => [.. gateways];

    private class FakeGateway(string name, GatewayResultType result) : IPaymentGateway
    {
        public string Name => name;
        public bool WasCalled { get; private set; }

        public Task<GatewayResult> ProcessAsync(
            Payment payment, string providerIdempotencyKey, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new GatewayResult(result, "00", result.ToString()));
        }

        public Task<GatewayResult> QueryAsync(string providerIdempotencyKey, CancellationToken ct = default)
            => Task.FromResult(new GatewayResult(result, "00", result.ToString()));
    }

    private class ThrowingGateway(string name) : IPaymentGateway
    {
        public string Name => name;

        public Task<GatewayResult> ProcessAsync(
            Payment payment, string providerIdempotencyKey, CancellationToken ct = default)
            => throw new TimeoutException("The gateway never answered.");

        public Task<GatewayResult> QueryAsync(string providerIdempotencyKey, CancellationToken ct = default)
            => Task.FromResult(new GatewayResult(GatewayResultType.Uncertain, "unknown", "Still unknown."));
    }
}

using PayMaestro.Application.Services;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Tests;

public class CascadeExecutorTests
{
    private readonly CascadeExecutor _cascade = new();

    [Fact]
    public async Task Approves_on_first_gateway_and_stops()
    {
        var payment = NewPayment();
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
        var payment = NewPayment();
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
        var payment = NewPayment();
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
        var payment = NewPayment();
        var route = Route(
            new FakeGateway("A", GatewayResultType.SoftDecline),
            new FakeGateway("B", GatewayResultType.Error));

        await _cascade.ExecuteAsync(payment, route);

        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Equal(2, payment.Attempts.Count);
    }

    private static Payment NewPayment() => Payment.Create(
        idempotencyKey: "key-1", merchantReference: "ORDER-1", customerId: "cust-1",
        amount: 100m, currency: "EUR", cardBin: "411111", cardLast4: "9999",
        cardCountry: "MT", customerIp: "203.0.113.10", ipCountry: "MT");

    private static List<IPaymentGateway> Route(params IPaymentGateway[] gateways) => [.. gateways];

    private class FakeGateway(string name, GatewayResultType result) : IPaymentGateway
    {
        public string Name => name;
        public bool WasCalled { get; private set; }

        public Task<GatewayResult> ProcessAsync(Payment payment, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new GatewayResult(result, "00", result.ToString()));
        }
    }
}
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

/// <summary>
/// Shared behaviour of the mock acquirers: latency, the provider-side idempotency contract and
/// the one case that makes reconciliation necessary — a charge that settles on the provider
/// while the answer is lost on the way back.
/// </summary>
public abstract class MockGateway(MockProviderLedger ledger) : IPaymentGateway
{
    /// <summary>Test card whose charge succeeds at the provider but never answers the caller.</summary>
    protected const string UnansweredCard = "9999";

    public abstract string Name { get; }

    protected abstract TimeSpan Latency { get; }

    protected abstract GatewayResult Decide(Payment payment);

    public async Task<GatewayResult> ProcessAsync(
        Payment payment, string providerIdempotencyKey, CancellationToken ct = default)
    {
        if (ledger.Find(providerIdempotencyKey) is { } alreadySettled)
            return alreadySettled;           // the provider recognises the key: no second charge

        await Task.Delay(Latency, ct);

        var settled = ledger.Settle(providerIdempotencyKey, Decide(payment));

        if (payment.CardLast4 == UnansweredCard)
            throw new TimeoutException($"{Name} accepted the charge but did not answer in time.");

        return settled;
    }

    public Task<GatewayResult> QueryAsync(string providerIdempotencyKey, CancellationToken ct = default)
        => Task.FromResult(ledger.Find(providerIdempotencyKey)
            ?? new GatewayResult(GatewayResultType.Error, "not_found", "The provider holds no record for this key."));
}

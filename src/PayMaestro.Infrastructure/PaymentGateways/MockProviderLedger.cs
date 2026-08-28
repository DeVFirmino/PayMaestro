using System.Collections.Concurrent;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

/// <summary>
/// Stands in for what a real acquirer keeps on its own side: the outcome it already produced
/// for an idempotency key. It is what makes the mock gateways honest about the contract —
/// presenting a key twice returns the first outcome instead of charging twice.
/// </summary>
public sealed class MockProviderLedger
{
    private readonly ConcurrentDictionary<string, GatewayResult> _settled = new();

    /// <summary>Stores the outcome for a key, or returns the one already stored.</summary>
    public GatewayResult Settle(string providerIdempotencyKey, GatewayResult result)
        => _settled.GetOrAdd(providerIdempotencyKey, result);

    public GatewayResult? Find(string providerIdempotencyKey)
        => _settled.TryGetValue(providerIdempotencyKey, out GatewayResult? settled) ? settled : null;
}

using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Gateways;

/// <summary>
/// Builds the key sent to a provider for one attempt. It is derived, never random, so the same
/// attempt always presents the same key — that is what lets a retry after an unknown outcome
/// be recognised by the provider instead of charging twice.
/// </summary>
public static class ProviderIdempotencyKey
{
    public static string For(Payment payment, string gatewayName, int attemptOrder)
        => $"{payment.IdempotencyKey}:{gatewayName}:{attemptOrder}";
}

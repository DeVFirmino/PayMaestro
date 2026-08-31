using System.Security.Cryptography;
using System.Text;
using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Gateways;

/// <summary>
/// Builds the key sent to a provider for one attempt. It is derived, never random, so the same
/// attempt always presents the same key — that is what lets a retry after an unknown outcome
/// be recognised by the provider instead of charging twice.
/// </summary>
public static class ProviderIdempotencyKey
{
    /// <summary>
    /// The merchant and the client key are hashed rather than concatenated. Both are
    /// caller-supplied and unbounded in practice, and a key that outgrew its column would throw
    /// after the reservation was already committed, leaving a payment stuck in Processing with
    /// no attempt to recover from. Hashing keeps the length constant whatever the caller sends.
    /// </summary>
    public static string For(Payment payment, string gatewayName, int attemptOrder)
        => $"{gatewayName}:{attemptOrder}:{Fingerprint(payment.MerchantId, payment.IdempotencyKey)}";

    private static string Fingerprint(string merchantId, string idempotencyKey)
    {
        // A unit separator keeps "ab" + "c" from colliding with "a" + "bc".
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{merchantId}\u001F{idempotencyKey}"));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

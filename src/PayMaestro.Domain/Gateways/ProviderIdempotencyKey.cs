using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Gateways;

/// <summary>
/// Builds the key sent to a provider for one attempt. It is derived, never random, so the same
/// attempt always presents the same key — that is what lets a retry after an unknown outcome
/// be recognised by the provider instead of charging twice.
/// </summary>
public readonly record struct ProviderIdempotencyKey(string Value)
{
    public const int MaxLength = 200;

    public static ProviderIdempotencyKey Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Provider idempotency key is required.", nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"Provider idempotency key cannot exceed {MaxLength} characters.", nameof(value));
        }

        return new ProviderIdempotencyKey(value);
    }

    public static string For(Payment payment, string gatewayName, int attemptOrder)
        => Create($"{payment.MerchantId}:{payment.IdempotencyKey}:{gatewayName}:{attemptOrder}").Value;

    public override string ToString() => Value;
}

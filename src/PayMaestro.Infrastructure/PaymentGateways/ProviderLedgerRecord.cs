using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

/// <summary>
/// One outcome a simulated acquirer produced for an idempotency key. It belongs to the provider
/// side of the simulation, not to the orchestrator's domain, which is why it lives next to the mocks.
/// </summary>
public sealed class ProviderLedgerRecord
{
    // EF Core materialises the entity through the private constructor and sets every
    // property afterwards, so the null-forgiving defaults never reach a caller.
    public string ProviderIdempotencyKey { get; private set; } = null!;

    public GatewayResultType ResultType { get; private set; }

    public string ResponseCode { get; private set; } = null!;

    public DateTime SettledAt { get; private set; }

    private ProviderLedgerRecord()
    {
    }

    public static ProviderLedgerRecord Create(string providerIdempotencyKey, GatewayResult result) => new()
    {
        ProviderIdempotencyKey = providerIdempotencyKey,
        ResultType = result.ResultType,
        ResponseCode = result.ResponseCode,
        SettledAt = DateTime.UtcNow,
    };

    public GatewayResult ToResult() => new(ResultType, ResponseCode);
}

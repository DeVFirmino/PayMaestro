namespace PayMaestro.Infrastructure.PaymentGateways.Http;

/// <summary>The body the orchestrator sends to an acquirer over HTTP.</summary>
public sealed record ProviderChargeRequest
{
    public required string GatewayName { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string CardLast4 { get; init; }
}

namespace PayMaestro.Infrastructure.PaymentGateways.Http;

/// <summary>The outcome an acquirer holds for one provider idempotency key.</summary>
public sealed record ProviderChargeResponse
{
    public required string ResultType { get; init; }

    public required string ResponseCode { get; init; }

    public required string Message { get; init; }
}

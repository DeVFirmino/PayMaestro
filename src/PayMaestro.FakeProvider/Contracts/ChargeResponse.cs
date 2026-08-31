namespace PayMaestro.FakeProvider.Contracts;

/// <summary>The outcome the acquirer holds for one idempotency key.</summary>
public sealed record ChargeResponse
{
    public required string ResultType { get; init; }

    public required string ResponseCode { get; init; }

    public required string Message { get; init; }
}

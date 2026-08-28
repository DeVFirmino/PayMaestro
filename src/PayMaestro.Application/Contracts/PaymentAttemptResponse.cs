namespace PayMaestro.Application.Contracts;

/// <summary>One gateway attempt made while processing a payment.</summary>
public sealed record PaymentAttemptResponse
{
    public required string GatewayName { get; init; }

    public required int AttemptOrder { get; init; }

    public required string ResultType { get; init; }

    public required string GatewayResponseCode { get; init; }

    public required int DurationMs { get; init; }
}

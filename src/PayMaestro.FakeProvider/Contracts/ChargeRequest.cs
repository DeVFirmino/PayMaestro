namespace PayMaestro.FakeProvider.Contracts;

/// <summary>What the orchestrator sends to the acquirer to move money.</summary>
public sealed record ChargeRequest
{
    public required string GatewayName { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    /// <summary>The last four digits select the scenario the fake acquirer plays.</summary>
    public required string CardLast4 { get; init; }
}

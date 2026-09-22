namespace PayMaestro.Application.Contracts;

/// <summary>The payments one recovery run settled, each in its new state with the attempts it recovered.</summary>
public sealed record RecoverOrphanedPaymentsResponse
{
    public required IReadOnlyList<PaymentResponse> Payments { get; init; }
}

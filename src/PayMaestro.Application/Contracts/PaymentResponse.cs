namespace PayMaestro.Application.Contracts;

/// <summary>Final state of a payment, including its full attempt history.</summary>
public sealed record PaymentResponse
{
    public required Guid Id { get; init; }

    public required string MerchantReference { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string CardLast4 { get; init; }

    public required string Status { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required List<PaymentAttemptResponse> Attempts { get; init; }
}

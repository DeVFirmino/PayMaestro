namespace PayMaestro.Application.Services;

public sealed record PaymentRequestFingerprint
{
    public required string RequestFingerprint { get; init; }

    public required string PaymentMethodToken { get; init; }
}

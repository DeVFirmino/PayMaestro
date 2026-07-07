namespace PayMaestro.Domain.Fraud;

/// <summary>Outcome of one fraud rule for one payment.</summary>
public record FraudVerdict(bool IsSuspicious, string? Details)
{
    public static FraudVerdict Clean { get; } = new(false, null);
    public static FraudVerdict Suspicious(string details) => new(true, details);
}

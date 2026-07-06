using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Fraud;

public interface IFraudRule
{
    string RuleName { get; }
    Task<FraudVerdict> EvaluateAsync(Payment payment, CancellationToken ct = default);
}

public record FraudVerdict(bool IsSuspicious, string? Details);
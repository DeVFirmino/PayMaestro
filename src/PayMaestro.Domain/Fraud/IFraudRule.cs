using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Fraud;

/// <summary>
/// A fraud screening rule evaluated before any gateway is contacted.
/// Implementations live outside the Domain (they may need repositories,
/// GeoIP services, etc.); adding a rule requires no use case change.
/// </summary>
public interface IFraudRule
{
    string RuleName { get; }

    Task<FraudVerdict> EvaluateAsync(Payment payment, CancellationToken cancellationToken);
}

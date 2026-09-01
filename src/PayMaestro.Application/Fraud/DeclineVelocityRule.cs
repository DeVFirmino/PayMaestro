using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Fraud;
using PayMaestro.Domain.Repositories.Payments;

namespace PayMaestro.Application.Fraud;

/// <summary>
/// Flags cards that keep getting declined — the classic card-testing /
/// stolen-card probing pattern, where a fraudster retries a card until
/// something goes through.
/// </summary>
public sealed class DeclineVelocityRule : IFraudRule
{
    private const int MaxRecentDeclines = 3;
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private readonly IPaymentReadOnlyRepository _payments;

    public DeclineVelocityRule(IPaymentReadOnlyRepository payments)
    {
        _payments = payments;
    }

    public string RuleName => "DeclineVelocity";

    public async Task<FraudVerdict> EvaluateAsync(Payment payment, CancellationToken cancellationToken)
    {
        int declines = await _payments.CountRecentDeclinedAttemptsAsync(
            payment.CardBin,
            payment.CardLast4,
            Window,
            cancellationToken);

        return declines >= MaxRecentDeclines
            ? FraudVerdict.Suspicious($"Card reached {declines} declined attempts within {Window.TotalHours}h.")
            : FraudVerdict.Clean;
    }
}

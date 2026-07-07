using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Fraud;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Application.Fraud;

/// <summary>
/// Flags cards that keep getting declined — the classic card-testing /
/// stolen-card probing pattern, where a fraudster retries a card until
/// something goes through.
/// </summary>
public class DeclineVelocityRule(IPaymentReadOnlyRepository readRepo) : IFraudRule
{
    private const int MaxRecentDeclines = 3;
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    public string RuleName => "DeclineVelocity";

    public async Task<FraudVerdict> EvaluateAsync(Payment payment, CancellationToken ct = default)
    {
        var declines = await readRepo.CountRecentDeclinedAttempts(payment.CardBin, payment.CardLast4, Window);

        return declines >= MaxRecentDeclines
            ? FraudVerdict.Suspicious($"Card reached {declines} declined attempts within {Window.TotalHours}h.")
            : FraudVerdict.Clean;
    }
}
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

/// <summary>
/// Shared behaviour of the mock acquirers: latency, the provider-side idempotency contract and
/// the one case that makes reconciliation necessary — a charge that settles on the provider
/// while the answer is lost on the way back.
/// </summary>
public abstract class MockGateway : IPaymentGateway
{
    /// <summary>Test card every mock acquirer treats as stolen: a hard decline, never retried elsewhere.</summary>
    protected const string StolenCard = "0000";

    /// <summary>Test card whose charge succeeds at the provider but never answers the caller.</summary>
    protected const string UnansweredCard = "9999";

    protected static readonly GatewayResult Approved = new(GatewayResultType.Approved, "00");
    protected static readonly GatewayResult StolenCardDecline = new(GatewayResultType.HardDecline, "43");
    protected static readonly GatewayResult InsufficientFunds = new(GatewayResultType.SoftDecline, "51");
    protected static readonly GatewayResult ProviderUnavailable = new(GatewayResultType.Error, "96");

    private static readonly GatewayResult NotFound = new(GatewayResultType.Error, "not_found");

    private readonly MockProviderLedger _ledger;

    protected MockGateway(MockProviderLedger ledger)
    {
        _ledger = ledger;
    }

    public abstract string Name { get; }

    protected abstract TimeSpan Latency { get; }

    public async Task<GatewayResult> ProcessAsync(
        Payment payment,
        string providerIdempotencyKey,
        CancellationToken cancellationToken)
    {
        if (_ledger.Find(providerIdempotencyKey) is { } alreadySettled)
        {
            return alreadySettled;          // the provider recognises the key: no second charge
        }

        await Task.Delay(Latency, cancellationToken);

        GatewayResult settled = _ledger.Settle(providerIdempotencyKey, Decide(payment));

        if (payment.CardLast4 == UnansweredCard)
        {
            throw new TimeoutException($"{Name} accepted the charge but did not answer in time.");
        }

        return settled;
    }

    public Task<GatewayResult> QueryAsync(string providerIdempotencyKey, CancellationToken cancellationToken)
        => Task.FromResult(_ledger.Find(providerIdempotencyKey) ?? NotFound);

    /// <summary>Every mock declines the stolen test card and approves anything it has no rule for.</summary>
    protected virtual GatewayResult Decide(Payment payment)
        => payment.CardLast4 == StolenCard ? StolenCardDecline : Approved;
}

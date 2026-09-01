using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

public sealed class BetaPayGateway : MockGateway
{
    private const string InsufficientFundsCard = "2222";

    public BetaPayGateway(MockProviderLedger ledger)
        : base(ledger)
    {
    }

    public override string Name => "BetaPay";

    protected override TimeSpan Latency => TimeSpan.FromMilliseconds(200);

    protected override GatewayResult Decide(Payment payment)
        => payment.CardLast4 == InsufficientFundsCard ? InsufficientFunds : base.Decide(payment);
}

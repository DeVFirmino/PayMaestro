using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

public sealed class AlphaPayGateway : MockGateway
{
    private const string InsufficientFundsCard = "1111";

    public AlphaPayGateway(MockProviderLedger ledger)
        : base(ledger)
    {
    }

    public override string Name => "AlphaPay";

    protected override TimeSpan Latency => TimeSpan.FromMilliseconds(150);

    protected override GatewayResult Decide(Payment payment)
        => payment.CardLast4 == InsufficientFundsCard ? InsufficientFunds : base.Decide(payment);
}

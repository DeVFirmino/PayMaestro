using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

public sealed class GammaPayGateway : MockGateway
{
    private const string UnavailableCard = "3333";

    public GammaPayGateway(MockProviderLedger ledger)
        : base(ledger)
    {
    }

    public override string Name => "GammaPay";

    protected override TimeSpan Latency => TimeSpan.FromMilliseconds(100);

    protected override GatewayResult Decide(Payment payment)
        => payment.CardLast4 == UnavailableCard ? ProviderUnavailable : base.Decide(payment);
}

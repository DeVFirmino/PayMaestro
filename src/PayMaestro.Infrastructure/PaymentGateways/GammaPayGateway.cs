using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

public sealed class GammaPayGateway : MockGateway
{
    public GammaPayGateway(MockProviderLedger ledger)
        : base(ledger)
    {
    }

    public override string Name => "GammaPay";

    protected override TimeSpan Latency => TimeSpan.FromMilliseconds(100);

    protected override GatewayResult Decide(Payment payment) => payment.CardLast4 switch
    {
        "0000" => new(GatewayResultType.HardDecline, "43", "Stolen card"),
        "3333" => new(GatewayResultType.Error, "96", "Gateway unavailable"),
        _ => new(GatewayResultType.Approved, "00", "Approved")
    };
}

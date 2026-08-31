using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

public sealed class AlphaPayGateway : MockGateway
{
    public AlphaPayGateway(MockProviderLedger ledger)
        : base(ledger)
    {
    }

    public override string Name => "AlphaPay";

    protected override TimeSpan Latency => TimeSpan.FromMilliseconds(150);

    protected override GatewayResult Decide(Payment payment) => payment.CardLast4 switch
    {
        "0000" => new(GatewayResultType.HardDecline, "43", "Stolen card"),
        "1111" => new(GatewayResultType.SoftDecline, "51", "Insufficient funds"),
        "2222" => new(GatewayResultType.SoftDecline, "51", "Route to BetaPay scenario"),
        "3333" => new(GatewayResultType.SoftDecline, "51", "Route to GammaPay scenario"),
        _ => new(GatewayResultType.Approved, "00", "Approved")
    };
}

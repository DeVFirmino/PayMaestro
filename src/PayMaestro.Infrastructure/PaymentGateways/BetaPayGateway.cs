using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

public class BetaPayGateway(MockProviderLedger ledger) : MockGateway(ledger)
{
    public override string Name => "BetaPay";

    protected override TimeSpan Latency => TimeSpan.FromMilliseconds(200);

    protected override GatewayResult Decide(Payment payment) => payment.CardLast4 switch
    {
        "0000" => new(GatewayResultType.HardDecline, "43", "Stolen card"),
        "2222" => new(GatewayResultType.SoftDecline, "51", "Insufficient funds"),
        _ => new(GatewayResultType.Approved, "00", "Approved")
    };
}

using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

public class BetaPayGateway : IPaymentGateway
{
    public string Name => "BetaPay";

    public async Task<GatewayResult> ProcessAsync(Payment payment, CancellationToken ct = default)
    {
        await Task.Delay(200, ct);

        if (payment.CardLast4 == "0000")
            return new(GatewayResultType.HardDecline, "43", "Stolen card");

        if (payment.CardLast4 == "2222")
            return new(GatewayResultType.SoftDecline, "51", "Insufficient funds");

        return new(GatewayResultType.Approved, "00", "Approved");
    }
}
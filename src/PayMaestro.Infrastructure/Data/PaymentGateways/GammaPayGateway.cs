using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways;

public class GammaPayGateway : IPaymentGateway
{
    public string Name => "GammaPay";

    public async Task<GatewayResult> ProcessAsync(Payment payment, CancellationToken ct = default)
    {
        await Task.Delay(100, ct);

        if (payment.CardLast4 == "0000")
            return new(GatewayResultType.HardDecline, "43", "Stolen card");

        if (payment.CardLast4 == "3333")
            return new(GatewayResultType.Error, "96", "Gateway unavailable");

        return new(GatewayResultType.Approved, "00", "Approved");
    }
}
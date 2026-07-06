using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Gateways;

public interface IPaymentGateway
{
    string Name { get; }
    Task<GatewayResult> ProcessAsync(Payment payment, CancellationToken ct = default);
}


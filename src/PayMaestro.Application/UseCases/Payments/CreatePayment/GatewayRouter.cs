using Microsoft.Extensions.Options;
using PayMaestro.Application.Options;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Application.UseCases.Payments.CreatePayment;

/// <summary>
/// Chooses which gateways may take a payment, and in what order. Eligibility (currency and
/// amount cap) and priority come from configuration, so adding an acquirer is one class and
/// one config entry.
/// </summary>
public sealed class GatewayRouter
{
    private readonly IOptions<GatewayRoutingOptions> _routing;
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public GatewayRouter(IOptions<GatewayRoutingOptions> routing, IEnumerable<IPaymentGateway> gateways)
    {
        _routing = routing;
        _gateways = gateways;
    }

    public IReadOnlyList<IPaymentGateway> RouteFor(Payment payment)
        => _routing.Value.Gateways
            .Where(route => route.SupportedCurrencies.Contains(payment.Currency)
                            && payment.Amount <= route.MaxAmount)
            .OrderBy(route => route.Priority)
            .Select(route => _gateways.FirstOrDefault(gateway => gateway.Name == route.Name))
            .OfType<IPaymentGateway>()
            .ToList();
}

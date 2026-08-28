namespace PayMaestro.Application.Options;

public sealed class GatewayRoutingOptions
{
    public List<GatewayRouteOptions> Gateways { get; set; } = [];
}

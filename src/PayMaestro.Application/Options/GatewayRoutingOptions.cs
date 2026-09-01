namespace PayMaestro.Application.Options;

public sealed class GatewayRoutingOptions
{
    public const string SectionName = "GatewayRouting";

    public List<GatewayRouteOptions> Gateways { get; set; } = [];
}

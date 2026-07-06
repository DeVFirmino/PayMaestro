namespace PayMaestro.Application.Options;

public class GatewayRoutingOptions
{
    public List<GatewayRouteOptions> Gateways { get; set; } = [];
}

public class GatewayRouteOptions
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public List<string> SupportedCurrencies { get; set; } = [];
    public decimal MaxAmount { get; set; }
}
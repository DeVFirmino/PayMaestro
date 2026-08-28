namespace PayMaestro.Application.Options;

public sealed class GatewayRouteOptions
{
    public string Name { get; set; } = string.Empty;

    public int Priority { get; set; }

    public List<string> SupportedCurrencies { get; set; } = [];

    public decimal MaxAmount { get; set; }
}

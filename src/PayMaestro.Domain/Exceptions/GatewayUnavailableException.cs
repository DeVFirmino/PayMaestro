using System.Net;

namespace PayMaestro.Domain.Exceptions;

public sealed class GatewayUnavailableException : PayMaestroException
{
    public GatewayUnavailableException(string gatewayName)
        : base($"Gateway '{gatewayName}' is not registered, so its outcome cannot be queried.")
    {
        GatewayName = gatewayName;
    }

    public string GatewayName { get; }

    public override HttpStatusCode StatusCode => HttpStatusCode.ServiceUnavailable;
}

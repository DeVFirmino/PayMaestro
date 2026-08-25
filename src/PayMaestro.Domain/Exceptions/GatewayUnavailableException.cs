namespace PayMaestro.Domain.Exceptions;

public class GatewayUnavailableException(string gatewayName)
    : PayMaestroException($"Gateway '{gatewayName}' is not registered, so its outcome cannot be queried.");

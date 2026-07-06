using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Gateways;

public record GatewayResult
    (GatewayResultType ResultType, string ResponseCode, string Message);
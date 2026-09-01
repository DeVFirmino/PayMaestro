using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Tests.Support;

/// <summary>A gateway whose charge call fails without an answer, like a dropped connection.</summary>
public sealed class ThrowingGateway : IPaymentGateway
{
    public ThrowingGateway(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public Task<GatewayResult> ProcessAsync(
        Payment payment,
        string providerIdempotencyKey,
        CancellationToken cancellationToken)
        => throw new TimeoutException("The gateway never answered.");

    public Task<GatewayResult> QueryAsync(string providerIdempotencyKey, CancellationToken cancellationToken)
        => Task.FromResult(new GatewayResult(GatewayResultType.Uncertain, "unknown"));
}

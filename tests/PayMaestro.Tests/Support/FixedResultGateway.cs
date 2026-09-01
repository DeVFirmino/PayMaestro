using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Tests.Support;

/// <summary>A gateway that always answers with one result and remembers whether it was asked.</summary>
public sealed class FixedResultGateway : IPaymentGateway
{
    private readonly GatewayResult _result;

    public FixedResultGateway(string name, GatewayResultType resultType)
    {
        Name = name;
        _result = new GatewayResult(resultType, "00");
    }

    public string Name { get; }

    public bool WasCalled { get; private set; }

    public Task<GatewayResult> ProcessAsync(
        Payment payment,
        string providerIdempotencyKey,
        CancellationToken cancellationToken)
    {
        WasCalled = true;

        return Task.FromResult(_result);
    }

    public Task<GatewayResult> QueryAsync(string providerIdempotencyKey, CancellationToken cancellationToken)
        => Task.FromResult(_result);
}

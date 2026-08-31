using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Tests.Support;

/// <summary>
/// A gateway that answers every query with one fixed outcome, and refuses to be charged. It
/// stands in for the acquirer a recovery pass talks to, where only the query path is used.
/// </summary>
public sealed class StaticQueryGateway : IPaymentGateway
{
    private readonly GatewayResultType _resultType;
    private readonly string _responseCode;

    public StaticQueryGateway(string name, GatewayResultType resultType, string responseCode)
    {
        Name = name;
        _resultType = resultType;
        _responseCode = responseCode;
    }

    public string Name { get; }

    /// <summary>How many keys have been queried so far.</summary>
    public int Queries { get; private set; }

    /// <summary>Runs after each query, with the key that was queried. Lets a test interleave a writer.</summary>
    public Func<string, Task>? AfterQuery { get; set; }

    public Task<GatewayResult> ProcessAsync(
        Payment payment, string providerIdempotencyKey, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Recovery must never charge; it may only query.");

    public async Task<GatewayResult> QueryAsync(
        string providerIdempotencyKey, CancellationToken cancellationToken = default)
    {
        Queries++;

        if (AfterQuery is not null)
        {
            await AfterQuery(providerIdempotencyKey);
        }

        return new GatewayResult(_resultType, _responseCode, "Queried by recovery.");
    }
}

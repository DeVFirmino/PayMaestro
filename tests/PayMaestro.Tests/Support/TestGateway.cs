using System.Collections.Concurrent;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Tests.Support;

/// <summary>
/// A gateway that counts how many times money actually moved and honours the provider-side
/// idempotency contract, so a test can assert "two requests, one charge" instead of trusting it.
/// </summary>
public sealed class TestGateway(string name, Func<Task>? whileCharging = null, bool answers = true) : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, GatewayResult> _settled = new();
    private int _charges;

    /// <summary>How many charges the provider actually accepted.</summary>
    public int Charges => Volatile.Read(ref _charges);

    public string Name => name;

    /// <summary>A gateway that settles the charge and then fails to answer, like a timeout.</summary>
    public static TestGateway Unanswering(string name) => new(name, answers: false);

    public async Task<GatewayResult> ProcessAsync(
        Payment payment, string providerIdempotencyKey, CancellationToken ct = default)
    {
        if (_settled.TryGetValue(providerIdempotencyKey, out var alreadySettled))
            return alreadySettled;              // recognised key: no second charge

        if (whileCharging is not null)
            await whileCharging();

        Interlocked.Increment(ref _charges);
        var result = new GatewayResult(GatewayResultType.Approved, "00", "Approved");
        _settled[providerIdempotencyKey] = result;

        if (!answers)
            throw new TimeoutException($"{name} took the charge but never answered.");

        return result;
    }

    public Task<GatewayResult> QueryAsync(string providerIdempotencyKey, CancellationToken ct = default)
        => Task.FromResult(_settled.TryGetValue(providerIdempotencyKey, out var settled)
            ? settled
            : new GatewayResult(GatewayResultType.Error, "not_found", "The provider holds no record for this key."));
}

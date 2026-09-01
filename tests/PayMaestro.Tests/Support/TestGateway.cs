using System.Collections.Concurrent;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Tests.Support;

/// <summary>
/// A gateway that counts how many times money actually moved and honours the provider-side
/// idempotency contract, so a test can assert "two requests, one charge" instead of trusting it.
/// </summary>
public sealed class TestGateway : IPaymentGateway
{
    private static readonly GatewayResult Approved = new(GatewayResultType.Approved, "00");
    private static readonly GatewayResult NotFound = new(GatewayResultType.Error, "not_found");

    private readonly ConcurrentDictionary<string, GatewayResult> _settled = new();
    private readonly Func<Task>? _whileCharging;
    private readonly bool _answers;
    private int _charges;

    public TestGateway(string name, Func<Task>? whileCharging = null, bool answers = true)
    {
        Name = name;
        _whileCharging = whileCharging;
        _answers = answers;
    }

    public string Name { get; }

    /// <summary>How many charges the provider actually accepted.</summary>
    public int Charges => Volatile.Read(ref _charges);

    /// <summary>A gateway that settles the charge and then fails to answer, like a timeout.</summary>
    public static TestGateway Unanswering(string name) => new(name, answers: false);

    public async Task<GatewayResult> ProcessAsync(
        Payment payment,
        string providerIdempotencyKey,
        CancellationToken cancellationToken)
    {
        if (_settled.TryGetValue(providerIdempotencyKey, out GatewayResult? alreadySettled))
        {
            return alreadySettled;          // recognised key: no second charge
        }

        if (_whileCharging is not null)
        {
            await _whileCharging();
        }

        Interlocked.Increment(ref _charges);
        _settled[providerIdempotencyKey] = Approved;

        if (_answers is false)
        {
            throw new TimeoutException($"{Name} took the charge but never answered.");
        }

        return Approved;
    }

    public Task<GatewayResult> QueryAsync(string providerIdempotencyKey, CancellationToken cancellationToken)
        => Task.FromResult(_settled.TryGetValue(providerIdempotencyKey, out GatewayResult? settled) ? settled : NotFound);
}

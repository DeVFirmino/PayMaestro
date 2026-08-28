using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Gateways;

public interface IPaymentGateway
{
    public string Name { get; }

    /// <summary>
    /// Charges the payment. <paramref name="providerIdempotencyKey"/> is the provider's own
    /// deduplication key: sending the same key twice must yield the first outcome, not a second charge.
    /// </summary>
    public Task<GatewayResult> ProcessAsync(Payment payment, string providerIdempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the provider what happened to a key it may already have seen. Used to settle a
    /// payment whose charge returned no answer.
    /// </summary>
    public Task<GatewayResult> QueryAsync(string providerIdempotencyKey, CancellationToken cancellationToken = default);
}

using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.PaymentRepository;

public interface IPaymentReadOnlyRepository
{
    public Task<Payment?> GetByMerchantAndIdempotencyKeyAsync(
        string merchantId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds one payment of one merchant. A merchant never reaches a payment of another
    /// merchant, even when it knows the identifier.
    /// </summary>
    public Task<Payment?> GetByMerchantAndIdAsync(
        string merchantId,
        Guid id,
        CancellationToken cancellationToken);

    public Task<IReadOnlyList<Payment>> ListWithStaleProcessingAttemptsAsync(
        DateTime cutoff,
        int take,
        CancellationToken cancellationToken);

    public Task<int> CountRecentDeclinedAttemptsAsync(
        string cardBin,
        string cardLast4,
        DateTime since,
        CancellationToken cancellationToken);
}

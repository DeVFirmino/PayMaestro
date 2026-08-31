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

    /// <summary>
    /// Counts recent declines on one card, for one merchant. The merchant scope is part of the
    /// rule, not an optimisation: without it a card declined at another merchant would reject
    /// this merchant's payment, and would leak that other merchant's card activity.
    /// </summary>
    public Task<int> CountRecentDeclinedAttemptsAsync(
        string merchantId,
        string cardBin,
        string cardLast4,
        DateTime since,
        CancellationToken cancellationToken);
}

using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.Payments;

public interface IPaymentReadOnlyRepository
{
    Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<int> CountRecentDeclinedAttemptsAsync(
        string cardBin,
        string cardLast4,
        TimeSpan window,
        CancellationToken cancellationToken);
}

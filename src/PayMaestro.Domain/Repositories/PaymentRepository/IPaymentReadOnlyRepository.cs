using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.PaymentRepository;

public interface IPaymentReadOnlyRepository
{
    public Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    public Task<int> CountRecentDeclinedAttemptsAsync(
        string cardBin,
        string cardLast4,
        TimeSpan window,
        CancellationToken cancellationToken);
}

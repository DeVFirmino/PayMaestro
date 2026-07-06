using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.PaymentRepository;

public interface IPaymentReadOnlyRepository
{
    Task<Payment?> GetByIdempotencyKey(string idempotencyKey);
    Task<Payment?> GetById(Guid id);
    Task<int> CountRecentDeclinedAttempts(string cardBin, string cardLast4, TimeSpan window);
}
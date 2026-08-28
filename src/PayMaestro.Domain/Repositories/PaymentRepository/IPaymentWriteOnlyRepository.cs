using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.PaymentRepository;

public interface IPaymentWriteOnlyRepository
{
    public Task AddAsync(Payment payment, CancellationToken cancellationToken);
}

using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.Payments;

public interface IPaymentWriteOnlyRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}

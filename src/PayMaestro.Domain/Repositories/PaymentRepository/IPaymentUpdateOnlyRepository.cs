using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.PaymentRepository;

public interface IPaymentUpdateOnlyRepository
{
    void Update(Payment payment);
}
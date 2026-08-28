using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.PaymentRepository;

public interface IPaymentUpdateOnlyRepository
{
    public void Update(Payment payment);
}

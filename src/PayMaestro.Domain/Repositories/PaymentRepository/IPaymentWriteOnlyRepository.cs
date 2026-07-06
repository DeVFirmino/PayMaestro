using PayMaestro.Domain.Entities;

namespace PayMaestro.Domain.Repositories.PaymentRepository;

public interface IPaymentWriteOnlyRepository
{
    Task Add(Payment payment);
}
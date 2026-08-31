using PayMaestro.Application.Contracts;

namespace PayMaestro.Application.UseCases.Payments.GetPaymentById;

public interface IGetPaymentByIdUseCase
{
    public Task<PaymentResponse?> Execute(string merchantId, Guid paymentId, CancellationToken cancellationToken = default);
}

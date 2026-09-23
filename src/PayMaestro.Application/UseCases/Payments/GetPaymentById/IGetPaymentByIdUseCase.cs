using PayMaestro.Application.Contracts;

namespace PayMaestro.Application.UseCases.Payments.GetPaymentById;

public interface IGetPaymentByIdUseCase
{
    Task<PaymentResponse> Execute(Guid paymentId, CancellationToken cancellationToken);
}

using PayMaestro.Application.Contracts;

namespace PayMaestro.Application.UseCases.Payments.CreatePayment;

public interface ICreatePaymentUseCase
{
    Task<PaymentResponse> Execute(
        string idempotencyKey,
        CreatePaymentRequest request,
        CancellationToken cancellationToken);
}

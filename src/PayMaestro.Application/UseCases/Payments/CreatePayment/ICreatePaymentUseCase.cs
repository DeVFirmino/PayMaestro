using PayMaestro.Application.Contracts;

namespace PayMaestro.Application.UseCases.Payments.CreatePayment;

public interface ICreatePaymentUseCase
{
    public Task<PaymentResponse> Execute(
        string merchantId,
        string idempotencyKey,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);
}

using PayMaestro.Application.Contracts;

namespace PayMaestro.Application.UseCases.Payments.ReconcilePayment;

public interface IReconcilePaymentUseCase
{
    public Task<PaymentResponse> Execute(string merchantId, Guid paymentId, CancellationToken cancellationToken = default);
}

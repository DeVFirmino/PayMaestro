using PayMaestro.Application.Contracts;

namespace PayMaestro.Application.UseCases.Payments.ReconcilePayment;

public interface IReconcilePaymentUseCase
{
    public Task<PaymentResponse> Execute(Guid paymentId, CancellationToken cancellationToken = default);
}

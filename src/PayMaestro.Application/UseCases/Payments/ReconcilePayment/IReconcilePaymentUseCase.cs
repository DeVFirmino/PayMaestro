using PayMaestro.Application.Contracts;

namespace PayMaestro.Application.UseCases.Payments.ReconcilePayment;

public interface IReconcilePaymentUseCase
{
    Task<PaymentResponse> Execute(Guid paymentId, CancellationToken cancellationToken);
}

using PayMaestro.Application.Contracts;

namespace PayMaestro.Application.UseCases.Payments.GetPaymentById;

public interface IGetPaymentByIdUseCase
{
    /// <summary>Returns null when no payment has this id; the endpoint answers an empty 404.</summary>
    Task<PaymentResponse?> Execute(Guid paymentId, CancellationToken cancellationToken);
}

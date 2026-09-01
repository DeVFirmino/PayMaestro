using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Repositories.Payments;

namespace PayMaestro.Application.UseCases.Payments.GetPaymentById;

public sealed class GetPaymentByIdUseCase : IGetPaymentByIdUseCase
{
    private readonly IPaymentReadOnlyRepository _payments;

    public GetPaymentByIdUseCase(IPaymentReadOnlyRepository payments)
    {
        _payments = payments;
    }

    public async Task<PaymentResponse?> Execute(Guid paymentId, CancellationToken cancellationToken)
    {
        Payment? payment = await _payments.GetByIdAsync(paymentId, cancellationToken);

        return payment?.ToResponse();
    }
}

using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Application.UseCases.Payments.GetPaymentById;

public sealed class GetPaymentByIdUseCase : IGetPaymentByIdUseCase
{
    private readonly IPaymentReadOnlyRepository _readRepository;

    public GetPaymentByIdUseCase(IPaymentReadOnlyRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<PaymentResponse?> Execute(string merchantId, Guid paymentId, CancellationToken cancellationToken = default)
    {
        Payment? payment = await _readRepository.GetByMerchantAndIdAsync(merchantId, paymentId, cancellationToken);
        return payment?.ToResponse();
    }
}

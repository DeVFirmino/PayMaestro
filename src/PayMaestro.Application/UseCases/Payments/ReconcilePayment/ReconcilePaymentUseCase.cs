using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Application.UseCases.Payments.ReconcilePayment;

/// <summary>
/// Settles a payment whose last gateway call returned no answer. It asks the provider what
/// happened to the key that attempt already used, rather than charging again to find out.
/// </summary>
public sealed class ReconcilePaymentUseCase : IReconcilePaymentUseCase
{
    private readonly IPaymentReadOnlyRepository _readRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public ReconcilePaymentUseCase(
        IPaymentReadOnlyRepository readRepository,
        IUnitOfWork unitOfWork,
        IEnumerable<IPaymentGateway> gateways)
    {
        _readRepository = readRepository;
        _unitOfWork = unitOfWork;
        _gateways = gateways;
    }

    public async Task<PaymentResponse> Execute(string merchantId, Guid paymentId, CancellationToken cancellationToken = default)
    {
        Payment payment = await _readRepository.GetByMerchantAndIdAsync(merchantId, paymentId, cancellationToken)
            ?? throw new PaymentNotFoundException(paymentId);

        if (payment.Status is PaymentStatus.Processing)
        {
            throw new PaymentInProgressException(payment.IdempotencyKey);
        }

        if (payment.Status is not PaymentStatus.RequiresReconciliation)
        {
            return payment.ToResponse(); // already settled: reconciling again changes nothing
        }

        PaymentAttempt lastAttempt = payment.Attempts.OrderByDescending(attempt => attempt.AttemptOrder).First();
        IPaymentGateway gateway = _gateways.FirstOrDefault(candidate => candidate.Name == lastAttempt.GatewayName)
                      ?? throw new GatewayUnavailableException(lastAttempt.GatewayName);

        GatewayResult outcome = await gateway.QueryAsync(lastAttempt.ProviderIdempotencyKey, cancellationToken);

        switch (outcome.ResultType)
        {
            case GatewayResultType.Approved:
                payment.Authorize();
                payment.Capture();
                break;

            case GatewayResultType.Uncertain:
                return payment.ToResponse(); // still unknown: leave it for the next attempt

            default:
                payment.Decline(); // the provider confirms no money moved
                break;
        }

        await _unitOfWork.CommitAsync(cancellationToken); // loses to a concurrent reconciler via the concurrency stamp

        return payment.ToResponse();
    }
}

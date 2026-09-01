using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.Payments;

namespace PayMaestro.Application.UseCases.Payments.ReconcilePayment;

/// <summary>
/// Settles a payment whose last gateway call returned no answer. It asks the provider what
/// happened to the key that attempt already used, rather than charging again to find out.
/// </summary>
public sealed class ReconcilePaymentUseCase : IReconcilePaymentUseCase
{
    private readonly IPaymentReadOnlyRepository _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public ReconcilePaymentUseCase(
        IPaymentReadOnlyRepository payments,
        IUnitOfWork unitOfWork,
        IEnumerable<IPaymentGateway> gateways)
    {
        _payments = payments;
        _unitOfWork = unitOfWork;
        _gateways = gateways;
    }

    public async Task<PaymentResponse> Execute(Guid paymentId, CancellationToken cancellationToken)
    {
        Payment payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new PaymentNotFoundException(paymentId);

        if (payment.Status is PaymentStatus.Processing)
        {
            throw new PaymentInProgressException(payment.IdempotencyKey);
        }

        if (payment.Status is not PaymentStatus.RequiresReconciliation)
        {
            return payment.ToResponse();    // already settled: reconciling again changes nothing
        }

        // A payment only reaches RequiresReconciliation through a gateway call, so it has an attempt.
        PaymentAttempt lastAttempt = payment.LastAttempt
            ?? throw new InvalidOperationException("A payment awaiting reconciliation has no gateway attempt.");

        IPaymentGateway gateway = _gateways.FirstOrDefault(candidate => candidate.Name == lastAttempt.GatewayName)
            ?? throw new GatewayUnavailableException(lastAttempt.GatewayName);

        GatewayResult outcome = await gateway.QueryAsync(lastAttempt.ProviderIdempotencyKey, cancellationToken);

        switch (outcome.ResultType)
        {
            case GatewayResultType.Approved:
                payment.AuthorizeAndCapture();
                break;

            case GatewayResultType.Uncertain:
                return payment.ToResponse();    // still unknown: leave it for the next attempt

            default:
                payment.Decline();              // the provider confirms no money moved
                break;
        }

        await _unitOfWork.CommitAsync(cancellationToken);   // loses to a concurrent reconciler via the concurrency stamp

        return payment.ToResponse();
    }
}

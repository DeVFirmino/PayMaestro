using PayMaestro.Application.Communication;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Application.Services;

/// <summary>
/// Settles a payment whose last gateway call returned no answer. It asks the provider what
/// happened to the key that attempt already used, rather than charging again to find out.
/// </summary>
public class PaymentReconciler(
    IPaymentReadOnlyRepository readRepo,
    IUnitOfWork unitOfWork,
    IEnumerable<IPaymentGateway> gateways)
{
    public async Task<ResponsePaymentJson> Reconcile(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await readRepo.GetById(paymentId) ?? throw new PaymentNotFoundException(paymentId);

        if (payment.Status is PaymentStatus.Processing)
            throw new PaymentInProgressException(payment.IdempotencyKey);

        if (payment.Status is not PaymentStatus.RequiresReconciliation)
            return payment.ToResponse();     // already settled: reconciling again changes nothing

        var lastAttempt = payment.Attempts.OrderByDescending(a => a.AttemptOrder).First();
        var gateway = gateways.FirstOrDefault(g => g.Name == lastAttempt.GatewayName)
                      ?? throw new GatewayUnavailableException(lastAttempt.GatewayName);

        var outcome = await gateway.QueryAsync(lastAttempt.ProviderIdempotencyKey, ct);

        switch (outcome.ResultType)
        {
            case GatewayResultType.Approved:
                payment.Authorize();
                payment.Capture();
                break;

            case GatewayResultType.Uncertain:
                return payment.ToResponse(); // still unknown: leave it for the next attempt

            default:
                payment.Decline();           // the provider confirms no money moved
                break;
        }

        await unitOfWork.Commit();           // loses to a concurrent reconciler via the concurrency stamp

        return payment.ToResponse();
    }
}

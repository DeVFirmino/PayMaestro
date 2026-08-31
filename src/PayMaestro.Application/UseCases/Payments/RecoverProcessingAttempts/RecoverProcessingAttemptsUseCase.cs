using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Application.UseCases.Payments.RecoverProcessingAttempts;

/// <summary>
/// Settles payments whose process stopped between committing an attempt and recording its
/// outcome. It asks the gateway what happened to the key that attempt already presented, and
/// never charges again.
/// </summary>
public sealed class RecoverProcessingAttemptsUseCase : IRecoverProcessingAttemptsUseCase
{
    private readonly IPaymentUpdateOnlyRepository _updateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public RecoverProcessingAttemptsUseCase(
        IPaymentUpdateOnlyRepository updateRepository,
        IUnitOfWork unitOfWork,
        IEnumerable<IPaymentGateway> gateways)
    {
        _updateRepository = updateRepository;
        _unitOfWork = unitOfWork;
        _gateways = gateways;
    }

    /// <summary>
    /// Each payment is committed on its own. One stale concurrency stamp must not discard the
    /// outcomes already queried for the payments before it, which is what a single commit for
    /// the whole batch would do — sending every one of them back to the gateway next pass.
    /// </summary>
    public async Task<int> Execute(DateTime cutoff, int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Payment> payments = await _updateRepository.ListWithStaleProcessingAttemptsAsync(
            cutoff,
            take,
            cancellationToken);

        int recovered = 0;
        foreach (Payment payment in payments)
        {
            PaymentAttempt? attempt = NewestProcessingAttempt(payment);
            IPaymentGateway? gateway = attempt is null
                ? null
                : _gateways.FirstOrDefault(candidate => candidate.Name == attempt.GatewayName);

            if (attempt is null || gateway is null)
            {
                continue;
            }

            GatewayResult outcome = await gateway.QueryAsync(attempt.ProviderIdempotencyKey, cancellationToken);
            attempt.Complete(outcome.ResultType, outcome.ResponseCode, attempt.DurationMs);
            Settle(payment, outcome.ResultType);

            try
            {
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch (ConcurrentPaymentModificationException)
            {
                // Another writer settled this payment first, so its outcome is the newer one.
                // The failed change is still tracked by this unit of work and would be replayed
                // into the next commit, so the pass ends here. Everything already committed
                // keeps its outcome, and the next pass starts from a clean unit of work.
                break;
            }

            recovered++;
        }

        return recovered;
    }

    private static PaymentAttempt? NewestProcessingAttempt(Payment payment)
        => payment.Attempts
            .Where(candidate => candidate.Status == PaymentAttemptStatus.Processing)
            .OrderByDescending(candidate => candidate.AttemptOrder)
            .FirstOrDefault();

    /// <summary>
    /// A recovered payment always reaches a terminal state or an explicit reconciliation state.
    /// It is never left in Processing: the request that owned its cascade is gone, so nothing
    /// would ever move it again and the merchant's idempotency key would answer 409 forever.
    /// <para>
    /// Declining on a soft decline or a gateway error is a deliberate divergence from the live
    /// cascade, which would carry those to the next acquirer. Recovery has no cascade left to
    /// continue; the merchant retries under a new idempotency key.
    /// </para>
    /// </summary>
    private static void Settle(Payment payment, GatewayResultType resultType)
    {
        switch (resultType)
        {
            case GatewayResultType.Approved:
                payment.Authorize();
                payment.Capture();
                break;

            case GatewayResultType.Uncertain:
                payment.MarkForReconciliation();
                break;

            default:
                payment.Decline();
                break;
        }
    }
}

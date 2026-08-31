using Microsoft.Extensions.Logging;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.PaymentRepository;

namespace PayMaestro.Application.UseCases.Payments.RecoverProcessingAttempts;

/// <summary>
/// Settles payments whose process stopped mid-flow, so no payment stays in Processing — where
/// its idempotency key would answer 409 forever and nothing would ever move it again.
/// <para>
/// A payment whose attempt was committed is resolved by asking the gateway what happened to
/// the key that attempt already presented; it is never charged again. A payment that never got
/// as far as an attempt is declined outright: the attempt row is committed before any gateway
/// call, so with no attempt there is nothing a gateway could have charged.
/// </para>
/// </summary>
public sealed class RecoverProcessingAttemptsUseCase : IRecoverProcessingAttemptsUseCase
{
    private readonly IPaymentUpdateOnlyRepository _updateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly ILogger<RecoverProcessingAttemptsUseCase> _logger;

    public RecoverProcessingAttemptsUseCase(
        IPaymentUpdateOnlyRepository updateRepository,
        IUnitOfWork unitOfWork,
        IEnumerable<IPaymentGateway> gateways,
        ILogger<RecoverProcessingAttemptsUseCase> logger)
    {
        _updateRepository = updateRepository;
        _unitOfWork = unitOfWork;
        _gateways = gateways;
        _logger = logger;
    }

    /// <summary>
    /// Each payment is committed on its own. One stale concurrency stamp must not discard the
    /// outcomes already queried for the payments before it, which is what a single commit for
    /// the whole batch would do — sending every one of them back to the gateway next pass.
    /// </summary>
    public async Task<int> Execute(DateTime cutoff, int take, CancellationToken cancellationToken = default)
    {
        int recovered = 0;

        IReadOnlyList<Payment> payments = await _updateRepository.ListWithStaleProcessingAttemptsAsync(
            cutoff,
            take,
            cancellationToken);

        foreach (Payment payment in payments)
        {
            PaymentAttempt attempt = NewestProcessingAttempt(payment);
            IPaymentGateway? gateway = _gateways.FirstOrDefault(candidate => candidate.Name == attempt.GatewayName);

            if (gateway is null)
            {
                // The gateway that took the attempt is no longer registered, so its outcome
                // cannot be queried automatically. Left in Processing the payment would occupy
                // the head of this batch forever and starve every payment behind it; parked in
                // reconciliation it waits, visibly, for an operator instead.
                payment.MarkForReconciliation();
                _logger.LogWarning(
                    "Gateway {GatewayName} is no longer registered; payment {PaymentId} was moved to reconciliation.",
                    attempt.GatewayName,
                    payment.Id);
            }
            else
            {
                GatewayResult outcome = await gateway.QueryAsync(attempt.ProviderIdempotencyKey, cancellationToken);
                attempt.Complete(outcome.ResultType, outcome.ResponseCode, attempt.DurationMs);
                Settle(payment, outcome.ResultType);
            }

            if (await TryCommit(cancellationToken) is false)
            {
                return recovered;
            }

            recovered++;
        }

        IReadOnlyList<Payment> orphaned = await _updateRepository.ListStaleProcessingWithoutAttemptsAsync(
            cutoff,
            take,
            cancellationToken);

        foreach (Payment payment in orphaned)
        {
            // The reservation was committed but the flow died before its cascade committed a
            // first attempt — a failing fraud rule, a failed commit, a stopped process. No
            // attempt means no gateway was contacted, so declining moves no money.
            payment.Decline();

            if (await TryCommit(cancellationToken) is false)
            {
                return recovered;
            }

            recovered++;
        }

        return recovered;
    }

    /// <summary>
    /// False means another writer settled this payment first, so its outcome is the newer one.
    /// The failed change is still tracked by this unit of work and would be replayed into the
    /// next commit, so the pass must end there. Everything already committed keeps its outcome,
    /// and the next pass starts from a clean unit of work.
    /// </summary>
    private async Task<bool> TryCommit(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (ConcurrentPaymentModificationException)
        {
            return false;
        }

        return true;
    }

    /// <summary>The stale-attempt query only returns payments that have one.</summary>
    private static PaymentAttempt NewestProcessingAttempt(Payment payment)
        => payment.Attempts
            .Where(candidate => candidate.Status == PaymentAttemptStatus.Processing)
            .OrderByDescending(candidate => candidate.AttemptOrder)
            .First();

    /// <summary>
    /// A recovered payment always reaches a terminal state or an explicit reconciliation state.
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

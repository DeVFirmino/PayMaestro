using Microsoft.Extensions.Options;
using PayMaestro.Application.Contracts;
using PayMaestro.Application.Options;
using PayMaestro.Application.UseCases.Payments.CreatePayment;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;
using PayMaestro.Domain.Repositories.Payments;

namespace PayMaestro.Application.UseCases.Payments.RecoverOrphanedPayments;

/// <summary>
/// Settles payments whose request ended before anything about a gateway call was saved: a
/// cancelled request, a crash, a failed final save. Such a payment stays Processing with no
/// attempt, so a retry answers 409 and reconciliation has no attempt to ask about.
/// Recovery walks the payment's route in cascade order and asks each provider about the key that
/// attempt would have carried, replaying the cascade's decisions from what the providers recorded.
/// It only ever queries: nothing here can charge.
/// </summary>
public sealed class RecoverOrphanedPaymentsUseCase : IRecoverOrphanedPaymentsUseCase
{
    private const string NoResponseCode = "no_response";

    // A recovered attempt is rebuilt from the provider's record; how long the original call took is not known.
    private const int UnknownDurationMs = 0;

    private readonly IPaymentReadOnlyRepository _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GatewayRouter _router;
    private readonly IOptions<PaymentRecoveryOptions> _options;

    public RecoverOrphanedPaymentsUseCase(
        IPaymentReadOnlyRepository payments,
        IUnitOfWork unitOfWork,
        GatewayRouter router,
        IOptions<PaymentRecoveryOptions> options)
    {
        _payments = payments;
        _unitOfWork = unitOfWork;
        _router = router;
        _options = options;
    }

    public async Task<RecoverOrphanedPaymentsResponse> Execute(CancellationToken cancellationToken)
    {
        DateTime reservedBefore = DateTime.UtcNow - _options.Value.OrphanThreshold;

        IReadOnlyList<Payment> orphans = await _payments.GetProcessingWithoutAttemptsAsync(reservedBefore, cancellationToken);

        List<PaymentResponse> recovered = [];

        foreach (Payment payment in orphans)
        {
            await RecoverAsync(payment, cancellationToken);

            // One commit per payment, guarded by its concurrency stamp: if the original request
            // settles it meanwhile, this write loses instead of overwriting the real outcome.
            await _unitOfWork.CommitAsync(cancellationToken);

            recovered.Add(payment.ToResponse());
        }

        return new RecoverOrphanedPaymentsResponse { Payments = recovered };
    }

    private async Task RecoverAsync(Payment payment, CancellationToken cancellationToken)
    {
        int order = 1;

        foreach (IPaymentGateway gateway in _router.RouteFor(payment))
        {
            string providerKey = ProviderIdempotencyKey.For(payment, gateway.Name, order);

            GatewayResult? record = await QueryAsync(gateway, providerKey, cancellationToken);
            if (record is null)
            {
                // This provider never took the charge. The cascade only moves on after an answer,
                // so no provider later in the route was ever asked either.
                break;
            }

            payment.RecordAttempt(PaymentAttempt.Create(
                payment.Id,
                gateway.Name,
                order,
                record.ResultType,
                record.ResponseCode,
                UnknownDurationMs,
                providerKey));
            order++;

            switch (record.ResultType)
            {
                case GatewayResultType.Approved:
                    payment.AuthorizeAndCapture();
                    return;

                case GatewayResultType.HardDecline:
                    payment.Decline();
                    return;

                case GatewayResultType.Uncertain:
                    payment.MarkForReconciliation();    // the attempt now exists, so reconcile can ask again
                    return;

                default:
                    continue;   // SoftDecline / Error: the cascade went on to the next gateway
            }
        }

        if (payment.Attempts.Count == 0)
        {
            payment.FailWithoutCharge();    // no provider on the route has any record of it
        }
        else
        {
            payment.Decline();              // every provider that saw it declined it
        }
    }

    /// <summary>A query that fails without an answer is an unknown outcome, as it is in the cascade.</summary>
    private static async Task<GatewayResult?> QueryAsync(
        IPaymentGateway gateway,
        string providerKey,
        CancellationToken cancellationToken)
    {
        try
        {
            return await gateway.QueryAsync(providerKey, cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested is false)
        {
            return new GatewayResult(GatewayResultType.Uncertain, NoResponseCode);
        }
    }
}

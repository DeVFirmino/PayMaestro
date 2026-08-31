using System.Diagnostics;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;
using PayMaestro.Domain.Repositories;

namespace PayMaestro.Application.Services;

/// <summary>
/// Tries each gateway in the route until one settles the payment.
/// Soft declines and gateway errors fall through to the next gateway;
/// a hard decline (fraud signal) stops the cascade immediately —
/// a suspected stolen card must never be retried on another acquirer.
/// An unknown outcome also stops the cascade: charging a second acquirer while the
/// first may have taken the money is exactly how a customer gets billed twice.
/// Every attempt is recorded on the payment for auditing.
/// </summary>
public sealed class CascadeExecutor
{
    private readonly IUnitOfWork _unitOfWork;

    public CascadeExecutor(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(
        Payment payment,
        IReadOnlyList<IPaymentGateway> route,
        CancellationToken cancellationToken = default)
    {
        int order = 1;

        foreach (IPaymentGateway gateway in route)
        {
            GatewayResult result = await TryGateway(payment, gateway, order++, cancellationToken);

            switch (result.ResultType)
            {
                case GatewayResultType.Approved:
                    payment.Authorize();
                    payment.Capture();
                    return;

                case GatewayResultType.HardDecline:
                    payment.Decline();
                    return;

                case GatewayResultType.Uncertain:
                    payment.MarkForReconciliation();
                    return;

                default:
                    continue; // SoftDecline / Error: the provider did not take the money
            }
        }

        payment.Decline(); // routes exhausted
    }

    private async Task<GatewayResult> TryGateway(
        Payment payment, IPaymentGateway gateway, int order, CancellationToken cancellationToken)
    {
        string providerKey = ProviderIdempotencyKey.For(payment, gateway.Name, order);
        PaymentAttempt attempt = PaymentAttempt.Start(payment.Id, gateway.Name, order, providerKey);
        payment.RecordAttempt(attempt);
        await _unitOfWork.CommitAsync(cancellationToken);

        long start = Stopwatch.GetTimestamp();

        GatewayResult result;
        try
        {
            result = await gateway.ProcessAsync(payment, providerKey, cancellationToken);
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested is false)
        {
            // The call failed without an answer. Whether the provider charged is unknown,
            // so it is recorded as such instead of being treated as a clean failure.
            result = new GatewayResult(GatewayResultType.Uncertain, "no_response", ex.Message);
        }

        attempt.Complete(
            result.ResultType,
            result.ResponseCode,
            (int)Stopwatch.GetElapsedTime(start).TotalMilliseconds);

        return result;
    }
}

using System.Diagnostics;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Application.UseCases.Payments.CreatePayment;

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
    private const string NoResponseCode = "no_response";

    public async Task ExecuteAsync(
        Payment payment,
        IReadOnlyList<IPaymentGateway> route,
        CancellationToken cancellationToken)
    {
        int order = 1;

        foreach (IPaymentGateway gateway in route)
        {
            GatewayResult result = await ChargeThroughAsync(payment, gateway, order, cancellationToken);
            order++;

            switch (result.ResultType)
            {
                case GatewayResultType.Approved:
                    payment.AuthorizeAndCapture();
                    return;

                case GatewayResultType.HardDecline:
                    payment.Decline();
                    return;

                case GatewayResultType.Uncertain:
                    payment.MarkForReconciliation();
                    return;

                default:
                    continue;   // SoftDecline / Error: the provider did not take the money
            }
        }

        payment.Decline();      // routes exhausted
    }

    /// <summary>Charges the payment through one gateway and records the attempt, whatever the answer.</summary>
    private static async Task<GatewayResult> ChargeThroughAsync(
        Payment payment,
        IPaymentGateway gateway,
        int order,
        CancellationToken cancellationToken)
    {
        string providerKey = ProviderIdempotencyKey.For(payment, gateway.Name, order);
        long start = Stopwatch.GetTimestamp();

        GatewayResult result;
        try
        {
            result = await gateway.ProcessAsync(payment, providerKey, cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested is false)
        {
            // The call failed without an answer. Whether the provider charged is unknown,
            // so it is recorded as such instead of being treated as a clean failure.
            result = new GatewayResult(GatewayResultType.Uncertain, NoResponseCode);
        }

        payment.RecordAttempt(PaymentAttempt.Create(
            payment.Id,
            gateway.Name,
            order,
            result.ResultType,
            result.ResponseCode,
            (int)Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            providerKey));

        return result;
    }
}

using System.Diagnostics;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

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
public class CascadeExecutor
{
    public async Task ExecuteAsync(Payment payment, IReadOnlyList<IPaymentGateway> route, CancellationToken ct = default)
    {
        var order = 1;

        foreach (var gateway in route)
        {
            var result = await TryGateway(payment, gateway, order++, ct);

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
                    continue;   // SoftDecline / Error: the provider did not take the money
            }
        }

        payment.Decline();       // routes exhausted
    }

    private static async Task<GatewayResult> TryGateway(
        Payment payment, IPaymentGateway gateway, int order, CancellationToken ct)
    {
        var providerKey = ProviderIdempotencyKey.For(payment, gateway.Name, order);
        var start = Stopwatch.GetTimestamp();

        GatewayResult result;
        try
        {
            result = await gateway.ProcessAsync(payment, providerKey, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // The call failed without an answer. Whether the provider charged is unknown,
            // so it is recorded as such instead of being treated as a clean failure.
            result = new GatewayResult(GatewayResultType.Uncertain, "no_response", ex.Message);
        }

        payment.RecordAttempt(PaymentAttempt.Create(
            payment.Id, gateway.Name, order, result.ResultType,
            result.ResponseCode, (int)Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            providerKey));

        return result;
    }
}

using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Application.Services;

/// <summary>
/// Tries each gateway in the route until one approves.
/// Soft declines and gateway errors fall through to the next gateway;
/// a hard decline (fraud signal) stops the cascade immediately —
/// a suspected stolen card must never be retried on another acquirer.
/// Every attempt is recorded on the payment for auditing.
/// </summary>
public class CascadeExecutor
{
    public async Task ExecuteAsync(Payment payment, IReadOnlyList<IPaymentGateway> route)
    {
        var order = 1;

        foreach (var gateway in route)
        {
            var result = await TryGateway(payment, gateway, order++);

            if (result.ResultType is GatewayResultType.Approved)
            {
                payment.Authorize();
                payment.Capture();
                return;
            }

            if (result.ResultType is GatewayResultType.HardDecline)
                break;                       // fraud signal: never retry elsewhere

            // SoftDecline / Error -> next gateway in the route
        }

        payment.Decline();                   // hard decline or routes exhausted
    }

    private static async Task<GatewayResult> TryGateway(Payment payment, IPaymentGateway gateway, int order)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await gateway.ProcessAsync(payment);
        sw.Stop();

        payment.RecordAttempt(PaymentAttempt.Create(
            payment.Id, gateway.Name, order, result.ResultType,
            result.ResponseCode, (int)sw.ElapsedMilliseconds));

        return result;
    }
}
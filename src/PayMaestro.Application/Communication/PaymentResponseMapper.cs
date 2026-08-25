using PayMaestro.Domain.Entities;

namespace PayMaestro.Application.Communication;

internal static class PaymentResponseMapper
{
    public static ResponsePaymentJson ToResponse(this Payment p) => new(
        p.Id, p.MerchantReference, p.Amount, p.Currency, p.CardLast4,
        p.Status.ToString(), p.CreatedAt,
        p.Attempts.OrderBy(a => a.AttemptOrder)
            .Select(a => new ResponseAttemptJson(a.GatewayName, a.AttemptOrder,
                a.ResultType.ToString(), a.GatewayResponseCode, a.DurationMs))
            .ToList());
}

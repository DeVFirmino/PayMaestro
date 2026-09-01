using PayMaestro.Domain.Entities;

namespace PayMaestro.Application.Contracts;

internal static class PaymentResponseMapper
{
    public static PaymentResponse ToResponse(this Payment payment) => new()
    {
        Id = payment.Id,
        MerchantReference = payment.MerchantReference,
        Amount = payment.Amount,
        Currency = payment.Currency,
        CardLast4 = payment.CardLast4,
        Status = payment.Status.ToString(),
        CreatedAt = payment.CreatedAt,
        Attempts = payment.Attempts
            .OrderBy(attempt => attempt.AttemptOrder)
            .Select(ToResponse)
            .ToList(),
    };

    private static PaymentAttemptResponse ToResponse(PaymentAttempt attempt) => new()
    {
        GatewayName = attempt.GatewayName,
        AttemptOrder = attempt.AttemptOrder,
        ResultType = attempt.ResultType.ToString(),
        GatewayResponseCode = attempt.GatewayResponseCode,
        DurationMs = attempt.DurationMs,
    };
}

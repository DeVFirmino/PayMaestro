using PayMaestro.Domain.Enums;

namespace PayMaestro.Domain.Entities;

public sealed class PaymentAttempt : EntityBase
{
    public Guid PaymentId { get; private set; }
    public string GatewayName { get; private set; } = null!;
    public int AttemptOrder { get; private set; }
    public PaymentAttemptStatus Status { get; private set; }
    public GatewayResultType ResultType { get; private set; }

    /// <summary>
    /// The key sent to the provider for this attempt. It is derived from the payment and the
    /// attempt, so re-driving the same attempt after an unknown outcome reaches the provider
    /// under the key it already saw instead of asking for a second charge.
    /// </summary>
    public string ProviderIdempotencyKey { get; private set; } = null!;

    public string GatewayResponseCode { get; private set; } = null!;
    public int DurationMs { get; private set; }

    private PaymentAttempt() { }

    public static PaymentAttempt Start(Guid paymentId, string gatewayName,
        int attemptOrder, string providerIdempotencyKey) => new()
        {
            PaymentId = paymentId,
            GatewayName = gatewayName,
            AttemptOrder = attemptOrder,
            Status = PaymentAttemptStatus.Processing,
            ResultType = GatewayResultType.Uncertain,
            GatewayResponseCode = "processing",
            DurationMs = 0,
            ProviderIdempotencyKey = providerIdempotencyKey
        };

    public void Complete(GatewayResultType resultType, string gatewayResponseCode, int durationMs)
    {
        Status = PaymentAttemptStatus.Completed;
        ResultType = resultType;
        GatewayResponseCode = gatewayResponseCode;
        DurationMs = durationMs;
    }
}

using PayMaestro.Domain.Enums;

namespace PayMaestro.Domain.Entities;

public class PaymentAttempt : EntityBase
{
    public Guid PaymentId { get; private set; }
    public string GatewayName { get; private set; } = null!;
    public int AttemptOrder { get; private set; }
    public GatewayResultType ResultType { get; private set; }
    
    public string GatewayResponseCode { get; private set; } = null!;
    public int DurationMs { get; private set; }

    private PaymentAttempt() { }

    public static PaymentAttempt Create(Guid paymentId, string gatewayName,
        int attemptOrder, GatewayResultType resultType, string gatewayResponseCode,
        int durationMs) => new()
    {
        PaymentId = paymentId,
        
        
        GatewayName = gatewayName,
        AttemptOrder = attemptOrder,
        ResultType = resultType,
        GatewayResponseCode = gatewayResponseCode,
        DurationMs = durationMs
    };
}
namespace PayMaestro.Application.Communication;

public record RequestCreatePaymentJson(
    string MerchantReference, string CustomerId, decimal Amount,
    string Currency, string CardNumber, string CustomerIp);

public record ResponseAttemptJson(
    string GatewayName, int AttemptOrder, string ResultType,
    string GatewayResponseCode, int DurationMs);

public record ResponsePaymentJson(
    Guid Id, string MerchantReference, decimal Amount, string Currency,
    string CardLast4, string Status, DateTime CreatedAt,
    List<ResponseAttemptJson> Attempts);
    
public record ResponseErrorJson(string Error);
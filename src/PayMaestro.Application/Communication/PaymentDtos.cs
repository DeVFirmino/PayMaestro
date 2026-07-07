using System.ComponentModel.DataAnnotations;

namespace PayMaestro.Application.Communication;

/// <summary>Data required to create and process a payment.</summary>
public record RequestCreatePaymentJson(
    [Required] string MerchantReference,
    [Required] string CustomerId,
    [Range(0.01, 1_000_000)] decimal Amount,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    [Required, StringLength(19, MinimumLength = 13)] string CardNumber,
    [Required] string CustomerIp);

/// <summary>One gateway attempt made while processing a payment.</summary>
public record ResponseAttemptJson(
    string GatewayName, int AttemptOrder, string ResultType,
    string GatewayResponseCode, int DurationMs);

/// <summary>Final state of a payment, including its full attempt history.</summary>
public record ResponsePaymentJson(
    Guid Id, string MerchantReference, decimal Amount, string Currency,
    string CardLast4, string Status, DateTime CreatedAt,
    List<ResponseAttemptJson> Attempts);

public record ResponseErrorJson(string Error);
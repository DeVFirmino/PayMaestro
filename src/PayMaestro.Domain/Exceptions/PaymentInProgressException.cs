using System.Net;

namespace PayMaestro.Domain.Exceptions;

/// <summary>
/// The idempotency key is reserved by a request that has not settled yet. Answering with a
/// second charge would be the bug this class exists to prevent, so the caller is told to retry.
/// </summary>
public sealed class PaymentInProgressException : PayMaestroException
{
    public PaymentInProgressException(string idempotencyKey)
        : base($"Payment for idempotency key '{idempotencyKey}' is still being processed. Retry to read its outcome.")
    {
        IdempotencyKey = idempotencyKey;
    }

    public string IdempotencyKey { get; }

    public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;
}

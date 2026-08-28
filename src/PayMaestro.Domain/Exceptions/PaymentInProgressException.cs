using System.Net;

namespace PayMaestro.Domain.Exceptions;

/// <summary>
/// The idempotency key is reserved by a request that has not settled yet. Answering with a
/// second charge would be the bug this class exists to prevent, so the caller is told to retry.
/// </summary>
public sealed class PaymentInProgressException : PayMaestroException
{
    public PaymentInProgressException(string key)
        : base($"Payment for idempotency key '{key}' is still being processed. Retry to read its outcome.")
    {
        Key = key;
    }

    public string Key { get; }

    public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;
}

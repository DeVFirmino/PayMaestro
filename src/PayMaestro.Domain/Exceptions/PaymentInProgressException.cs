namespace PayMaestro.Domain.Exceptions;

/// <summary>
/// The idempotency key is reserved by a request that has not settled yet. Answering with a
/// second charge would be the bug this class exists to prevent, so the caller is told to retry.
/// </summary>
public class PaymentInProgressException(string key)
    : PayMaestroException($"Payment for idempotency key '{key}' is still being processed. Retry to read its outcome.");

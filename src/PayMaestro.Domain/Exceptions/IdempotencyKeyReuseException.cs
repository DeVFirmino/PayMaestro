using System.Net;

namespace PayMaestro.Domain.Exceptions;

public sealed class IdempotencyKeyReuseException : PayMaestroException
{
    public IdempotencyKeyReuseException(string idempotencyKey)
        : base($"Idempotency key '{idempotencyKey}' was already used with a different payload.")
    {
        IdempotencyKey = idempotencyKey;
    }

    public string IdempotencyKey { get; }

    public override HttpStatusCode StatusCode => HttpStatusCode.UnprocessableEntity;
}

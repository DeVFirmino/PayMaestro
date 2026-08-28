using System.Net;

namespace PayMaestro.Domain.Exceptions;

public sealed class IdempotencyKeyReuseException : PayMaestroException
{
    public IdempotencyKeyReuseException(string key)
        : base($"Idempotency key '{key}' was already used with a different payload.")
    {
        Key = key;
    }

    public string Key { get; }

    public override HttpStatusCode StatusCode => HttpStatusCode.UnprocessableEntity;
}

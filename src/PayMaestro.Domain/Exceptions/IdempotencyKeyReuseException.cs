namespace PayMaestro.Domain.Exceptions;

public class IdempotencyKeyReuseException(string key)
    : PayMaestroException($"Idempotency key '{key}' was already used with a different payload.");
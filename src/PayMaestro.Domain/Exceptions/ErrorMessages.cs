namespace PayMaestro.Domain.Exceptions;

public static class ErrorMessages
{
    public const string IdempotencyKeyHeaderRequired = "Idempotency-Key header is required.";
    public const string IdempotencyKeyRequired = "Idempotency key is required.";
    public const string AmountMustBePositive = "Amount must be greater than zero.";
    public const string CurrencyMustBeIsoCode = "Currency must be a 3-letter ISO code.";
    public const string ConcurrentModification = "The payment was modified by another operation. Reload it and retry.";
    public const string UniqueConstraintViolation = "A record with the same unique key already exists.";
    public const string UnexpectedError = "Unexpected error.";
}

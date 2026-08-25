namespace PayMaestro.Domain.Exceptions;

public class PaymentNotFoundException(Guid id)
    : PayMaestroException($"No payment exists with id '{id}'.");

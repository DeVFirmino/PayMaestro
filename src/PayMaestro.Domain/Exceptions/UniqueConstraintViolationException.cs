namespace PayMaestro.Domain.Exceptions;

public class UniqueConstraintViolationException()
    : PayMaestroException("A record with the same unique key already exists.");
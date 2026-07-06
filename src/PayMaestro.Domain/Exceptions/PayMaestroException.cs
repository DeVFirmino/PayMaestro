using PayMaestro.Domain.Enums;

namespace PayMaestro.Domain.Exceptions;

public abstract class PayMaestroException(string message) : Exception(message);

 
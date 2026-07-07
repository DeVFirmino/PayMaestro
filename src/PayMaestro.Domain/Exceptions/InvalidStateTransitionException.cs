using PayMaestro.Domain.Enums;
namespace PayMaestro.Domain.Exceptions;

public class InvalidStateTransitionException(PaymentStatus from, PaymentStatus to)
    : PayMaestroException($"Invalid payment state transition: {from} -> {to}.");
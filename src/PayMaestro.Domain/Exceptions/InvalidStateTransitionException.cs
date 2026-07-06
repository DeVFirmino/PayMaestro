using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Exceptions;

public class InvalidStateTransitionException(PaymentStatus from, PaymentStatus to)
    : PayMaestroException($"Invalid payment state transition: {from} -> {to}.");
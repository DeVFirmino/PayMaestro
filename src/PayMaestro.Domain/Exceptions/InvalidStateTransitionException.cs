using System.Net;
using PayMaestro.Domain.Enums;

namespace PayMaestro.Domain.Exceptions;

public sealed class InvalidStateTransitionException : PayMaestroException
{
    public InvalidStateTransitionException(PaymentStatus from, PaymentStatus to)
        : base($"Invalid payment state transition: {from} -> {to}.")
    {
        From = from;
        To = to;
    }

    public PaymentStatus From { get; }

    public PaymentStatus To { get; }

    public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;
}

using System.Net;

namespace PayMaestro.Domain.Exceptions;

public sealed class PaymentNotFoundException : PayMaestroException
{
    public PaymentNotFoundException(Guid id)
        : base($"No payment exists with id '{id}'.")
    {
        PaymentId = id;
    }

    public Guid PaymentId { get; }

    public override HttpStatusCode StatusCode => HttpStatusCode.NotFound;
}

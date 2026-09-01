using System.Net;

namespace PayMaestro.Domain.Exceptions;

public sealed class PaymentNotFoundException : PayMaestroException
{
    public PaymentNotFoundException(Guid paymentId)
        : base($"No payment exists with id '{paymentId}'.")
    {
        PaymentId = paymentId;
    }

    public Guid PaymentId { get; }

    public override HttpStatusCode StatusCode => HttpStatusCode.NotFound;
}

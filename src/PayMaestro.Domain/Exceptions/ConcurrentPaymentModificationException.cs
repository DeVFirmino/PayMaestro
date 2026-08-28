using System.Net;

namespace PayMaestro.Domain.Exceptions;

/// <summary>
/// Another writer settled this payment first. The losing update is discarded rather than
/// overwriting an outcome it never saw.
/// </summary>
public sealed class ConcurrentPaymentModificationException : PayMaestroException
{
    public ConcurrentPaymentModificationException()
        : base("The payment was modified by another operation. Reload it and retry.")
    {
    }

    public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;
}

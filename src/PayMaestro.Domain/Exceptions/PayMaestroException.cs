using System.Net;

namespace PayMaestro.Domain.Exceptions;

/// <summary>
/// Every failure the API answers deliberately. The status code travels with the exception so
/// the exception filter maps it without a switch that has to learn each new subclass.
/// </summary>
public abstract class PayMaestroException : Exception
{
    protected PayMaestroException(string message)
        : base(message)
    {
    }

    protected PayMaestroException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public abstract HttpStatusCode StatusCode { get; }
}

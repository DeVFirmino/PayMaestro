using System.Net;

namespace PayMaestro.Domain.Exceptions;

public abstract class PayMaestroException : Exception
{
    protected PayMaestroException(string message)
        : base(message)
    {
    }

    public abstract HttpStatusCode StatusCode { get; }

    public virtual IReadOnlyList<string> Errors => [Message];
}

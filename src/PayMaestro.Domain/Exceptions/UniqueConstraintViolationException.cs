using System.Net;

namespace PayMaestro.Domain.Exceptions;

public sealed class UniqueConstraintViolationException : PayMaestroException
{
    public UniqueConstraintViolationException()
        : base("A record with the same unique key already exists.")
    {
    }

    public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;
}

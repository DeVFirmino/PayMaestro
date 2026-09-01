using System.Net;

namespace PayMaestro.Domain.Exceptions;

public sealed class ValidationFailedException : PayMaestroException
{
    public ValidationFailedException(string message)
        : base(message)
    {
    }

    public override HttpStatusCode StatusCode => HttpStatusCode.BadRequest;
}

using System.Net;

namespace PayMaestro.Domain.Exceptions;

/// <summary>
/// Lets a use case detect a race on a unique key, such as two concurrent requests carrying
/// the same idempotency key, without referencing EF Core.
/// </summary>
public sealed class UniqueConstraintViolationException : PayMaestroException
{
    public UniqueConstraintViolationException(Exception innerException)
        : base(ErrorMessages.UniqueConstraintViolation, innerException)
    {
    }

    public override HttpStatusCode StatusCode => HttpStatusCode.BadRequest;
}

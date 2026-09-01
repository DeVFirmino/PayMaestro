using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Exceptions;

namespace PayMaestro.API.Filters;

/// <summary>
/// The one place an exception becomes an HTTP response. Known failures carry their own status
/// code; anything else is a bug and answers 500 without leaking its message.
/// </summary>
public sealed class ExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ExceptionFilter> _logger;

    public ExceptionFilter(ILogger<ExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is PayMaestroException known)
        {
            _logger.LogWarning(known, "Handled failure on {Path}", context.HttpContext.Request.Path);

            context.Result = new ObjectResult(new ErrorResponse { Error = known.Message })
            {
                StatusCode = (int)known.StatusCode,
            };
        }
        else
        {
            _logger.LogError(
                context.Exception,
                "Unhandled failure {TraceIdentifier} on {Path}",
                context.HttpContext.TraceIdentifier,
                context.HttpContext.Request.Path);

            context.Result = new ObjectResult(new ErrorResponse { Error = ErrorMessages.UnexpectedError })
            {
                StatusCode = StatusCodes.Status500InternalServerError,
            };
        }

        context.ExceptionHandled = true;
    }
}

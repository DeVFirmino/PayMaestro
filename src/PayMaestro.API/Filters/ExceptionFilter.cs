using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PayMaestro.Application.Contracts;
using PayMaestro.Domain.Exceptions;

namespace PayMaestro.API.Filters;

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

            int statusCode = (int)known.StatusCode;
            context.HttpContext.Response.StatusCode = statusCode;
            context.Result = new ObjectResult(new ErrorResponse { Error = known.Message })
            {
                StatusCode = statusCode
            };
            context.ExceptionHandled = true;
            return;
        }

        if (context.Exception is ArgumentException argumentException)
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Result = new BadRequestObjectResult(new ErrorResponse { Error = argumentException.Message });
            context.ExceptionHandled = true;
            return;
        }

        string correlationId = context.HttpContext.TraceIdentifier;

        _logger.LogError(
            context.Exception,
            "Unhandled failure {CorrelationId} on {Path}",
            correlationId,
            context.HttpContext.Request.Path);

        context.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Result = new ObjectResult(new ErrorResponse { Error = "Unexpected error." })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
        context.ExceptionHandled = true;
    }
}

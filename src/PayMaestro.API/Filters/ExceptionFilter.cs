using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PayMaestro.Application.Communication;
using PayMaestro.Domain.Exceptions;

namespace PayMaestro.API.Filters;

public class ExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        context.Result = context.Exception switch
        {
            IdempotencyKeyReuseException e => new UnprocessableEntityObjectResult(new ResponseErrorJson(e.Message)),
            InvalidStateTransitionException e => new ConflictObjectResult(new ResponseErrorJson(e.Message)),
            PayMaestroException e => new BadRequestObjectResult(new ResponseErrorJson(e.Message)),
            ArgumentException e => new BadRequestObjectResult(new ResponseErrorJson(e.Message)),
            _ => new ObjectResult(new ResponseErrorJson("Unexpected error.")) { StatusCode = 500 }
        };
        context.ExceptionHandled = true;
    }
}
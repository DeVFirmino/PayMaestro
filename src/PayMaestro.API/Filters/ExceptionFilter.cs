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
            PaymentNotFoundException e => new NotFoundObjectResult(new ResponseErrorJson(e.Message)),
            IdempotencyKeyReuseException e => new UnprocessableEntityObjectResult(new ResponseErrorJson(e.Message)),

            // Both mean "someone else is already handling this payment" — the caller retries
            // and reads the outcome instead of triggering a second charge.
            PaymentInProgressException e => new ConflictObjectResult(new ResponseErrorJson(e.Message)),
            ConcurrentPaymentModificationException e => new ConflictObjectResult(new ResponseErrorJson(e.Message)),

            InvalidStateTransitionException e => new ConflictObjectResult(new ResponseErrorJson(e.Message)),
            GatewayUnavailableException e => new ObjectResult(new ResponseErrorJson(e.Message)) { StatusCode = 503 },
            PayMaestroException e => new BadRequestObjectResult(new ResponseErrorJson(e.Message)),
            ArgumentException e => new BadRequestObjectResult(new ResponseErrorJson(e.Message)),
            _ => new ObjectResult(new ResponseErrorJson("Unexpected error.")) { StatusCode = 500 }
        };
        context.ExceptionHandled = true;
    }
}

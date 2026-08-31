using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using PayMaestro.Application.Contracts;
using PayMaestro.Application.UseCases.Payments.CreatePayment;
using PayMaestro.Application.UseCases.Payments.GetPaymentById;
using PayMaestro.Application.UseCases.Payments.ReconcilePayment;

namespace PayMaestro.API.Controllers;

[ApiController]
[Route("api/payments")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("per-merchant")]
public sealed class PaymentsController : ControllerBase
{
    /// <summary>Creates and processes a payment.</summary>
    /// <remarks>
    /// Requires an <c>Idempotency-Key</c> header. The key is reserved in the database before any
    /// gateway is contacted, so a duplicate request never reaches a second charge: a completed key
    /// replays its stored result, and a key still in flight returns <c>409</c>.
    /// </remarks>
    /// <response code="200">Payment processed; the body contains the final status and every gateway attempt.</response>
    /// <response code="400">Missing or invalid request fields, or missing Idempotency-Key header.</response>
    /// <response code="409">The same Idempotency-Key is still being processed by another request.</response>
    /// <response code="422">The Idempotency-Key was already used with a different payload.</response>
    [HttpPost]
    [Authorize(Policy = "payments:write")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromServices] ICreatePaymentUseCase useCase,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(
                title: "Invalid idempotency key",
                detail: "Idempotency-Key header is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        PaymentResponse response = await useCase.Execute(
            GetMerchantId(),
            idempotencyKey,
            request,
            cancellationToken);
        return Ok(response);
    }

    /// <summary>Settles a payment whose gateway call returned no answer.</summary>
    /// <remarks>
    /// Asks the provider what happened to the idempotency key that attempt already used, instead
    /// of charging again to find out. Payments in any other state are returned unchanged.
    /// </remarks>
    /// <response code="200">The payment's current state after reconciliation.</response>
    /// <response code="404">No payment exists with this id.</response>
    /// <response code="409">The payment is still being processed by its original request.</response>
    /// <response code="503">The gateway that took the attempt is no longer registered.</response>
    [HttpPost("{id:guid}/reconcile")]
    [Authorize(Policy = "payments:reconcile")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Reconcile(
        [FromServices] IReconcilePaymentUseCase useCase,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => Ok(await useCase.Execute(GetMerchantId(), id, cancellationToken));

    /// <summary>Gets a payment by id, including its gateway attempt history.</summary>
    /// <response code="200">The payment was found.</response>
    /// <response code="404">No payment exists with this id.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "payments:read")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromServices] IGetPaymentByIdUseCase useCase,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        PaymentResponse? response = await useCase.Execute(GetMerchantId(), id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    private string GetMerchantId()
        => User.FindFirstValue("merchant_id")
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated merchant identity is missing.");
}

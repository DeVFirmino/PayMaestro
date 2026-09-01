using Microsoft.AspNetCore.Mvc;
using PayMaestro.Application.Contracts;
using PayMaestro.Application.UseCases.Payments.CreatePayment;
using PayMaestro.Application.UseCases.Payments.GetPaymentById;
using PayMaestro.Application.UseCases.Payments.ReconcilePayment;

namespace PayMaestro.API.Controllers;

[ApiController]
[Route("api/payments")]
[Produces("application/json")]
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
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromServices] ICreatePaymentUseCase useCase,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        PaymentResponse response = await useCase.Execute(idempotencyKey, request, cancellationToken);

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
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Reconcile(
        [FromServices] IReconcilePaymentUseCase useCase,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        PaymentResponse response = await useCase.Execute(id, cancellationToken);

        return Ok(response);
    }

    /// <summary>Gets a payment by id, including its gateway attempt history.</summary>
    /// <response code="200">The payment was found.</response>
    /// <response code="404">No payment exists with this id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromServices] IGetPaymentByIdUseCase useCase,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        PaymentResponse? response = await useCase.Execute(id, cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }
}

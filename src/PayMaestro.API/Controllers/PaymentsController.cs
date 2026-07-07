using Microsoft.AspNetCore.Mvc;
using PayMaestro.Application.Communication;
using PayMaestro.Application.Services;

namespace PayMaestro.API.Controllers;

[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public class PaymentsController(PaymentOrchestrator orchestrator) : ControllerBase
{
    /// <summary>Creates and processes a payment.</summary>
    /// <remarks>
    /// Requires an <c>Idempotency-Key</c> header. Sending the same key again
    /// replays the original result instead of charging the customer twice.
    /// </remarks>
    /// <response code="200">Payment processed; the body contains the final status and every gateway attempt.</response>
    /// <response code="400">Missing or invalid request fields, or missing Idempotency-Key header.</response>
    /// <response code="422">The Idempotency-Key was already used with a different payload.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ResponsePaymentJson), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] RequestCreatePaymentJson request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new ResponseErrorJson("Idempotency-Key header is required."));

        var response = await orchestrator.CreatePayment(idempotencyKey, request);
        return Ok(response);
    }

    /// <summary>Gets a payment by id, including its gateway attempt history.</summary>
    /// <response code="200">The payment was found.</response>
    /// <response code="404">No payment exists with this id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ResponsePaymentJson), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await orchestrator.GetById(id);
        return response is null ? NotFound() : Ok(response);
    }
}
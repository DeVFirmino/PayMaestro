using Microsoft.AspNetCore.Mvc;
using PayMaestro.FakeProvider.Contracts;
using PayMaestro.FakeProvider.Ledger;

namespace PayMaestro.FakeProvider.Controllers;

/// <summary>
/// The HTTP surface of the fake acquirer. It is deliberately small: take a charge under an
/// idempotency key, and tell a caller what happened to a key it already sent.
/// </summary>
[ApiController]
[Route("provider/charges")]
[Produces("application/json")]
public sealed class ChargesController : ControllerBase
{
    /// <summary>Takes one charge. The same Idempotency-Key never moves money twice.</summary>
    /// <response code="200">The acquirer holds an outcome for this key.</response>
    /// <response code="400">The Idempotency-Key header is missing.</response>
    /// <response code="504">The charge settled at the acquirer, but the answer is lost.</response>
    [HttpPost]
    public IActionResult Charge(
        [FromServices] ChargeLedger ledger,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] ChargeRequest request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(
                title: "Invalid idempotency key",
                detail: "Idempotency-Key header is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ChargeResponse? known = ledger.Find(idempotencyKey);
        if (known is not null)
        {
            return Ok(known); // recognised key: the first outcome, no second charge
        }

        ChargeResponse settled = ledger.Settle(
            idempotencyKey,
            request.GatewayName,
            ChargeScenarios.Decide(request.GatewayName, request.CardLast4));

        if (request.CardLast4 == ChargeScenarios.UnansweredCard)
        {
            // The money moved and the ledger holds it, but the caller learns nothing.
            // Only a query on the same key can tell the caller what happened.
            return StatusCode(StatusCodes.Status504GatewayTimeout);
        }

        return Ok(settled);
    }

    /// <summary>Reports what the acquirer did for one idempotency key.</summary>
    /// <response code="200">The acquirer holds an outcome for this key.</response>
    /// <response code="404">The acquirer holds no record for this key. No money moved.</response>
    [HttpGet("{idempotencyKey}")]
    public IActionResult Query(
        [FromServices] ChargeLedger ledger,
        [FromRoute] string idempotencyKey)
    {
        ChargeResponse? known = ledger.Find(idempotencyKey);
        return known is null ? NotFound() : Ok(known);
    }
}

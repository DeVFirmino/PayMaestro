using Microsoft.AspNetCore.Mvc;
using PayMaestro.Application.Communication;
using PayMaestro.Application.Services;

namespace PayMaestro.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(PaymentOrchestrator orchestrator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] RequestCreatePaymentJson request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency-Key header is required." });

        var response = await orchestrator.Execute(idempotencyKey, request);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await orchestrator.GetById(id);
        return response is null ? NotFound() : Ok(response);
    }
}
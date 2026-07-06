# PayMaestro

Payment orchestration API that routes card payments across multiple payment gateways with automatic failover (cascade), idempotency guarantees, and full attempt auditing.

Built with **.NET 10**, **ASP.NET Core**, **EF Core (SQLite)** and organized with **Clean Architecture**.

## How it works

1. A payment request comes in with an `Idempotency-Key` header.
2. If the key was already used, the stored result is replayed — **no double charge**. Reusing a key with a different amount or merchant reference is rejected.
3. The router builds an ordered list of eligible gateways from configuration (currency support, amount limit, priority).
4. The **cascade executor** tries each gateway in order:
   - **Approved** → payment is authorized and captured, done.
   - **SoftDecline / Error** (e.g. insufficient funds, gateway down) → try the next gateway.
   - **HardDecline** (fraud signal, e.g. stolen card) → stop immediately, never retry elsewhere.
5. Every gateway attempt is recorded (gateway, order, result, response code, duration) and returned in the response.

## Project structure

```
src/
  PayMaestro.Domain/          Entities (Payment, PaymentAttempt, FraudFlag),
                              enums, gateway/repository abstractions, domain exceptions
  PayMaestro.Application/     PaymentOrchestrator, CascadeExecutor, DTOs, routing options
  PayMaestro.Infrastructure/  EF Core DbContext, repositories, unit of work,
                              simulated gateways (AlphaPay, BetaPay, GammaPay)
  PayMaestro.API/             Controllers, exception filter, composition root
tests/
  PayMaestro.Tests/           xUnit test project
```

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet run --project src/PayMaestro.API
```

Swagger UI is available at `/swagger`. The SQLite database (`paymaestro.db`) is created automatically on first run.

## API

### Create a payment

```bash
curl -X POST http://localhost:5000/api/payments \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: order-123" \
  -d '{
    "merchantReference": "ORDER-123",
    "customerId": "cust-42",
    "amount": 250.00,
    "currency": "EUR",
    "cardNumber": "4111111111119999",
    "customerIp": "203.0.113.10"
  }'
```

Response includes the final status and the full attempt trail:

```json
{
  "id": "…",
  "merchantReference": "ORDER-123",
  "amount": 250.00,
  "currency": "EUR",
  "cardLast4": "9999",
  "status": "Captured",
  "attempts": [
    { "gatewayName": "AlphaPay", "attemptOrder": 1, "resultType": "Approved", "gatewayResponseCode": "00", "durationMs": 152 }
  ]
}
```

### Get a payment

```bash
curl http://localhost:5000/api/payments/{id}
```

## Gateway routing

Routing is configuration-driven (`appsettings.json`) — no code change needed to add, remove, or reprioritize gateways:

```json
"GatewayRouting": {
  "Gateways": [
    { "Name": "AlphaPay", "Priority": 1, "SupportedCurrencies": ["EUR", "GBP"], "MaxAmount": 5000 },
    { "Name": "BetaPay",  "Priority": 2, "SupportedCurrencies": ["EUR", "USD"], "MaxAmount": 10000 },
    { "Name": "GammaPay", "Priority": 3, "SupportedCurrencies": ["EUR", "USD", "GBP"], "MaxAmount": 2000 }
  ]
}
```

## Simulating gateway outcomes

The three built-in gateways are simulators. The **last 4 digits of the card number** control the outcome:

| Card ends in | AlphaPay | BetaPay | GammaPay |
|---|---|---|---|
| `0000` | Hard decline (stolen card) | Hard decline | Hard decline |
| `1111` | Soft decline (insufficient funds) | Approved | Approved |
| `2222` | Approved | Soft decline | Approved |
| `3333` | Approved | Approved | Error (gateway unavailable) |
| anything else | Approved | Approved | Approved |

Example: a `…1111` card in EUR is soft-declined by AlphaPay, then cascades to BetaPay and is approved — the response shows both attempts.

## Error handling

A global `ExceptionFilter` maps domain exceptions to HTTP responses:

- Missing `Idempotency-Key` header → `400 Bad Request`
- Idempotency key reuse with a different payload → `422 Unprocessable Entity`
- Invalid payment state transition → `409 Conflict`
- Other domain/validation errors → `400 Bad Request`
# PayMaestro

[![CI](https://github.com/DeVFirmino/PayMaestro/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/DeVFirmino/PayMaestro/actions/workflows/ci.yml)

A .NET payment API that chooses a provider, handles declines and avoids charging twice when a request is repeated. The providers are simulated and no real money moves.

.NET 10 · ASP.NET Core · EF Core · SQLite · xUnit

[Run it](#run-it) · [Try a payment](#try-one-payment) · [How it works](docs/payment-guide.md)

## What happens to a payment

- A provider temporarily declines: try the next eligible provider.
- A provider gives a hard decline: stop.
- A provider stops answering: check what happened before attempting another charge.
- A completed request is sent again with the same key and payload: return its stored result.

## Run it

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then:

```bash
git clone https://github.com/DeVFirmino/PayMaestro.git
cd PayMaestro
dotnet run --project src/PayMaestro.API
```

Open [Swagger](http://localhost:5225/swagger). The default Development profile creates the local SQLite database and supplies the development configuration.

## Try one payment

With the API running, paste this into another terminal:

```bash
curl -i http://localhost:5225/api/payments \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: 93747c29-68d8-4d3c-b0bb-0ddab07ac1e5' \
  -d '{
    "merchantReference": "ORDER-001",
    "customerId": "customer-42",
    "amount": 50,
    "currency": "EUR",
    "cardNumber": "4111111111111111",
    "customerIp": "185.89.10.20"
  }'
```

On a fresh database with the default configuration, expect HTTP `200` and status `Captured`: AlphaPay declines this card temporarily, then BetaPay approves it.

Run the same command again. It returns the same payment without another charge. Keep the key but change the amount to `60`, and the API returns `422` because the request no longer matches.

## Tests

From the repository root:

```bash
dotnet test
```

The tests cover concurrent requests, provider failures, recovery after a restart and the HTTP contract. [See the test scenarios](docs/payment-guide.md#tests).

## Scope and details

This is a study project. It has no authentication, uses service-wide idempotency keys and requires an API call to trigger reconciliation or recovery. Refunds are not implemented.

- [Payment flow and architecture](docs/payment-guide.md#the-flow-of-one-payment)
- [Endpoints and responses](docs/payment-guide.md#api)
- [More requests to try](docs/payment-guide.md#run-it)
- [Known limits](docs/payment-guide.md#known-limits)
- [Design specification](docs/SPEC.md)

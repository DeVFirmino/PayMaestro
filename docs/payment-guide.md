# Payment behaviour and reference

[Back to the README](../README.md)

PayMaestro is a study project in .NET: a payment flow with simulated providers, built around not charging twice when a request is repeated or times out.

What it does not have: the providers are simulated inside the same process and no real money moves, there is no merchant authentication, and the idempotency key is unique across the whole service rather than per merchant.

One API stands in front of several payment gateways. The sections below explain how it behaves when a request is repeated, when a provider does not answer, and when a request dies halfway.

## What the software does

- It accepts one card payment request at a time.
- It selects a payment provider (a "gateway") that supports the currency and the amount.
- It sends the charge to that gateway.
- If the gateway refuses for a temporary reason, it tries the next gateway.
- If a card shows a fraud pattern, it rejects the payment before any gateway call.
- It stores a record of every attempt, so each result can be explained later.

I built it over a weekend, spec-first, to learn how orchestration platforms serve the iGaming space. The design decisions are in [docs/SPEC.md](SPEC.md).

## The flow of one payment

![Flow diagram showing how PayMaestro reserves an idempotency key, screens a payment, tries eligible gateways, and stores one final result](architecture.svg)

*Editable source: [`docs/architecture.excalidraw`](architecture.excalidraw). Open it on [excalidraw.com](https://excalidraw.com) and export the SVG again after changes.*

## Idempotency: one key, at most one charge

Every request must send an `Idempotency-Key` header.

- The service inserts the payment row, in status `Processing`, **before** it contacts any gateway. This insert is committed first. It claims the key.
- When two requests race with the same key, a unique index picks one winner. The loser has charged nothing. It reads the winner's row and answers from it.
- A key that is still in flight returns `409 Conflict`. The service does not charge again.
- A finished key replays the stored result: the same payment id, status, attempt list and UTC creation time.
- The same key with a different payload returns `422`. The compared fields are amount, currency, customer, merchant reference, customer IP and the card. The card is compared by fingerprint, so two cards with the same BIN and last four digits are still different cards. The second one gets `422` and never reaches a provider.
- Each gateway attempt sends a derived key: `{idempotency-key}:{gateway}:{attempt}`. The provider can then recognise a retry of the same attempt.

## Card fingerprint

The full card number is never stored. To compare cards on replay, the service computes an HMAC-SHA256 of the card number when the request arrives, under a key from configuration (`CardFingerprint:Key`), and stores that next to the BIN and last four digits. Without the key the fingerprint cannot be reversed or recomputed from a guessed number.

- In Development, `appsettings.Development.json` carries a key that starts with `development-only-`. It is public and signs nothing real.
- Outside Development the application refuses to start unless a key of at least 32 characters is set, and it refuses the development key. Set it with the `CardFingerprint__Key` environment variable, for example `export CardFingerprint__Key="$(openssl rand -base64 48)"`.

## Fraud screening

Fraud rules implement the `IFraudRule` contract in the Domain layer. They run after the key is reserved and before any gateway call. Each hit is stored as a `FraudFlag` row.

One rule is active: **decline velocity**. It counts declined attempts for one card, identified by its keyed fingerprint. Three or more declines in 24 hours block the card. The payment ends `FraudRejected` with zero gateway calls.

## Routing and the cascade

The gateway list lives in `appsettings.json`: name, priority, supported currencies and amount cap. The service tries the eligible gateways in priority order. This is the cascade. The rules are:

- **Soft decline** (for example insufficient funds) or **gateway error**: try the next eligible gateway.
- **Hard decline** (for example a stolen card): stop at once. No other gateway is tried.
- **No answer**: stop and set the payment to `RequiresReconciliation`. A second charge could bill the customer twice.
- **No eligible gateway** for the currency or amount: the payment is `Declined` with zero attempts.

## Reconciliation

`POST /api/payments/{id}/reconcile` settles a payment whose charge got no answer. It asks the same gateway what happened to the derived key that attempt already used. It never charges again.

- The provider reports the charge as approved: the payment becomes `Captured`.
- The provider has no record of the key: the payment becomes `Declined`. No money moved.
- The mock providers keep their records in a `ProviderLedger` table in the same SQLite database, not in memory. A payment that was waiting before a restart still settles from the provider's record.
- The provider still gives no answer: the payment does not change.
- The payment is already settled: the call changes nothing and returns `200`.
- Two reconcilers work on the same payment: a concurrency stamp makes the one with stale data lose with `409`.

## Recovery of payments stuck in Processing

A request can end after it reserved its key and before it saved any gateway attempt: the client cancels, the process crashes, or the final save fails. The payment is then `Processing` with no attempt, so a retry gets `409` and reconcile has no attempt to ask about.

`POST /api/payments/recovery` settles those payments. It takes every `Processing` payment older than `PaymentRecovery:OrphanThreshold` (15 minutes by default) that has no attempt. For each one it walks the payment's route in cascade order and asks each provider about the derived key that attempt would have used (`{key}:{gateway}:{attempt}`). It only queries. It never charges.

- A provider recorded an approval: the attempt is saved and the payment becomes `Captured`.
- A provider recorded a hard decline: the payment becomes `Declined`.
- A provider recorded a soft decline or an error: the cascade would have moved on, so recovery asks the next provider.
- A provider has no record: the cascade never got past it, so no later provider was charged either. If no provider has a record, the payment becomes `FailedWithoutCharge`.
- A provider gives no answer to the query: an `Uncertain` attempt is saved and the payment becomes `RequiresReconciliation`, so reconcile can ask again.

The threshold has to be longer than any request can take. A payment whose request is still charging must not be settled before that request has its answer.

## Audit records

- Every gateway attempt is stored: gateway name, order, result code, duration and the derived key. Attempts with no answer are stored too.
- Only the card BIN (first six digits), the last four digits and the card fingerprint are stored. The full card number is never stored.
- The attempt and fraud-flag tables point to the payment with `Restrict` foreign keys. The database refuses to delete a payment that still has attempts or flags.

## Payment lifecycle

The `Payment` aggregate guards its own state transitions. An invalid transition throws a domain exception.

- Main path: `Pending → Processing → Authorized → Captured`.
- Terminal branches: `Declined`, `FraudRejected` and `FailedWithoutCharge`.
- `RequiresReconciliation` holds a charge with no answer. Reconciliation settles it to `Captured` or `Declined`.
- `FailedWithoutCharge` closes a payment whose request died before any attempt was saved, when recovery finds no provider with a record of it.
- There is no path back from `Captured`. Refunds are not implemented.

## API

| Endpoint | What it does | Answers |
|---|---|---|
| `POST /api/payments` | Creates and processes a payment. Requires the `Idempotency-Key` header. | `200` with status and attempt list · `400` invalid input or missing header · `409` key still in flight · `422` key reused with a different payload |
| `GET /api/payments/{id}` | Returns the payment and its attempt list. | `200` · `404` with an empty body |
| `POST /api/payments/{id}/reconcile` | Settles a payment whose charge got no answer. | `200` · `404` unknown id · `409` still processing · `503` gateway no longer registered |
| `POST /api/payments/recovery` | Settles payments stuck in `Processing` with no attempt, from the providers' records. | `200` with the payments it settled · `409` a payment was settled by its own request meanwhile; run it again |

## Architecture

Clean architecture: `API → Application → Domain ← Infrastructure`.

- **Domain**: entities, the state machine and all contracts: gateways, fraud rules, the card fingerprinter, repositories and the unit of work. No external references.
- **Application**: one use case class per operation, plus the `GatewayRouter`, which picks the eligible gateways, the `CascadeExecutor`, which owns the cascade policy, and the HMAC card fingerprinter.
- **Infrastructure**: EF Core with SQLite and versioned migrations, the three mock gateways and their provider ledger.
- **API**: MVC controllers, a global exception filter and Swagger.

## Tests

```bash
dotnet test
```

71 xUnit tests cover:

- the payment state machine;
- the cascade policy: approve, soft decline, hard decline, exception and unknown outcome;
- the idempotency race: two concurrent requests against a real SQLite file, one charge only ([`IdempotencyReservationTests`](../tests/PayMaestro.Tests/IdempotencyReservationTests.cs));
- replay, `409` and `422` answers, including the replayed UTC timestamp and a card that differs only in the middle digits ([`CreatePaymentReplayTests`](../tests/PayMaestro.Tests/CreatePaymentReplayTests.cs));
- the card fingerprint and the startup check on its key;
- reconciliation, including the stale concurrent reconciler and a reconcile through a fresh provider ledger after a restart ([`ReconciliationTests`](../tests/PayMaestro.Tests/ReconciliationTests.cs));
- recovery of payments stuck in `Processing`: settled from a provider record, failed without charge, cascade order, threshold and payments it must leave alone ([`OrphanRecoveryTests`](../tests/PayMaestro.Tests/OrphanRecoveryTests.cs));
- the decline-velocity threshold, 24-hour window and cards sharing a BIN and last four digits;
- the HTTP error contract over a real pipeline (`WebApplicationFactory`), including validation and `404` responses.

## Run it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The database is a local SQLite file, created on the first run in Development. Development also brings its own card fingerprint key, so nothing needs to be configured. Outside Development, set `CardFingerprint__Key` (see [Card fingerprint](#card-fingerprint)) and apply the EF Core migrations as a deployment step.

Run commands from the repository root.

```bash
dotnet run --project src/PayMaestro.API
```

The API listens on `http://localhost:5225`. With the default launch profile, the browser opens Swagger. Otherwise, open `http://localhost:5225/swagger`.

Send `POST /api/payments` with the header `Idempotency-Key: <any-uuid>` and this body:

```json
{
  "merchantReference": "ORDER-001",
  "customerId": "customer-42",
  "amount": 50,
  "currency": "EUR",
  "cardNumber": "4111111111111111",
  "customerIp": "185.89.10.20"
}
```

Note: this card ends in `1111`, so AlphaPay soft-declines it and BetaPay approves it. The response is `200 Captured` with a trail of two attempts. The last digits of the card select the mock behaviour:

| Card ending | Behaviour |
|---|---|
| `0000` | Hard decline everywhere. The cascade stops and the payment is `Declined` |
| `1111` | Soft decline on AlphaPay. The cascade moves to BetaPay |
| `2222` | Soft decline on BetaPay |
| `3333` | Gateway error on GammaPay |
| `9999` | The charge settles at the provider, but no answer comes back. The payment is `RequiresReconciliation` |
| anything else | Approved on the first eligible gateway |

More things to try:

- Send `6000` EUR. AlphaPay has a `5000` cap, so BetaPay takes the charge directly.
- Send a `JPY` amount. No gateway supports it. The payment is `Declined` with zero attempts.
- Send the same `Idempotency-Key` again after completion. The stored result comes back. Change the amount, currency, customer, merchant reference, IP, or any digit of the card, and the answer is `422`.
- Decline card `…0000` three times. The fourth payment on that card is `FraudRejected` with an empty attempt list.
- Send card `…9999`, then call `POST /api/payments/{id}/reconcile`. The payment becomes `Captured` with no second charge. Stop and restart the API between the two calls and the result is the same.
- Call `POST /api/payments/recovery`. With no payment stuck in `Processing` for more than 15 minutes, it answers `200` with an empty list.

## Known limits

- Stored `FraudFlag` rows are not part of any API response. A `FraudRejected` payment returns an empty attempt list and does not name the rule.
- Recovery does not run by itself. Something has to call `POST /api/payments/recovery`, by hand or on a schedule.
- Recovery derives the provider keys from the current gateway configuration. If the route of a payment changed between its request and the recovery run, the keys differ and recovery can miss a provider record.
- There is no authentication. Any caller can create, read or reconcile any payment, and the idempotency key is unique across the whole service, not per merchant. A service with more than one merchant would need a merchant identity on every route and keys scoped to it.
- A replayed response carries the same data, but the number format of `amount` can differ from the first response (for example `50` and `50.0`).

## Related certifications

- [iGaming Academy — Anti-Fraud & Payments Handling (2026)](certificates/Daniel_Silva_Anti_Fraud_and_Payments_Handling_2026.pdf)
- iGaming Academy — Anti-Money Laundering and Counter Terrorist Financing for Online Operators (2026)

## What's next

More fraud rules on the same `IFraudRule` contract, as specified in [docs/SPEC.md](SPEC.md): geo mismatch and amount anomaly. Then refunds, and a deployment to Azure Container Apps. My [Sports Betting API](https://github.com/DeVFirmino/SportsBetting) was deployed there by hand, with Azure SQL, in September 2026, and is [online](https://sportsbetting-api.nicewave-b8afa4cf.westeurope.azurecontainerapps.io/swagger/index.html), scaling to zero when idle. Its [deployment notes](https://github.com/DeVFirmino/SportsBetting#deployment) list the steps.

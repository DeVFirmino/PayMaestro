# PayMaestro — Design Spec

The decisions behind the code, written before/while building. Short on purpose.

## Goal

One API in front of many payment gateways. The merchant integrates once; PayMaestro picks the route, retries safely when a route fails, and keeps evidence of everything.

**Non-goals (for now):** real acquirer integrations, 3-D Secure, refund execution, multi-currency settlement.

## Domain model

- **Payment** — the aggregate root. Holds the money data, the card fingerprint (BIN + last 4 only — never the full PAN), and the status. All state transitions are methods on the entity with guards; an illegal transition throws.
- **PaymentAttempt** — one try on one gateway: gateway name, order, result, response code, duration. Immutable once written.
- **FraudFlag** — which fraud rule fired and why.

### Status state machine

```
Pending ──> Processing           (the idempotency key is reserved in the database)
Processing ──> FraudRejected     (a fraud rule fired; no gateway contacted)
Processing ──> Declined          (hard decline, or all routes exhausted)
Processing ──> Authorized ──> Captured ──> Refunded (roadmap)
Processing ──> RequiresReconciliation (a gateway never answered; outcome unknown)
RequiresReconciliation ──> Captured | Declined   (settled from the provider's own record)
```

## Idempotency

- The client sends an `Idempotency-Key` header.
- Same completed key + same payload → replay the stored result without another gateway call.
- Same key + different payload → `422`.
- The key has a **unique index in the database**, and the payment row is inserted in `Processing` **before any gateway call**. A request that loses that insert race has not contacted a provider yet, so it replays the winner's outcome instead of charging.
- Same key while the first request is still in flight → `409`. The caller retries and reads the outcome; it never triggers a second charge.
- Each attempt presents the provider a derived key, `{idempotency-key}:{gateway}:{attempt-order}`, so re-driving an attempt reaches the provider under a key it has already seen.
- The payment carries a **concurrency stamp**; a writer working from a stale copy loses instead of overwriting a settled outcome.
- **Remaining boundary:** the gateways are in-process mocks. They implement the provider side of the contract (a settled key returns its stored outcome, and can be queried), but this is not evidence against a real acquirer's API.

## Routing

Configuration-driven (`appsettings.json`): each gateway declares supported currencies, a max amount, and a priority. A payment's route = every eligible gateway, ordered by priority. Adding an acquirer = one gateway class + one config entry.

## Cascade policy

| Gateway result | Meaning | Action |
|---|---|---|
| `Approved` | Done | Authorize + capture, stop |
| `SoftDecline` | Recoverable (insufficient funds…) | Try next gateway |
| `Error` | Gateway problem, not card problem | Try next gateway |
| `HardDecline` | Fraud signal (stolen/blocked card) | **Stop immediately** — never shop a bad card to another acquirer |
| `Uncertain` | No answer (timeout, dropped connection) | **Stop immediately** and hold for reconciliation — the provider may already hold the money |

## Fraud screening

Rules implement `IFraudRule` (Domain contract) and run **before any gateway is contacted**. Every hit is stored as a `FraudFlag`; any hit ends the payment as `FraudRejected`.

| Rule | Status | Logic |
|---|---|---|
| Decline velocity | **Implemented** | Same card with 3+ declined attempts in 24h → reject (card-testing pattern) |
| Geo mismatch | Specified | IP country ≠ card country → flag (needs real BIN/GeoIP data first) |
| Amount anomaly | Specified | Amount far above the customer's usual pattern → flag |

## Audit

Every attempt and every fraud flag is persisted; deletes are restricted at the database level. If an acquirer or regulator asks for evidence (an RFI), `GET /api/payments/{id}` returns the full trail.

## Error mapping

| Case | HTTP |
|---|---|
| Invalid fields / missing idempotency header | 400 |
| Key reused with different payload | 422 |
| Illegal state transition | 409 |
| Same key still being processed | 409 |
| Payment settled by a concurrent writer | 409 |
| Payment not found (reconcile / get) | 404 |
| Gateway of a recorded attempt no longer registered | 503 |
| Unexpected | 500, generic message |

## Deliberate simplifications

In-process simulated gateways; stubbed card/IP countries; SQLite, with `EnsureCreated` on startup for the demo while migrations carry the schema; auto-capture immediately after authorization; reconciliation is triggered by an endpoint rather than a background sweeper, so a process that dies mid-charge leaves a `Processing` row for an operator to reconcile.

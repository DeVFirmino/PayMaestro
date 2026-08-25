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
Pending ──> FraudRejected        (a fraud rule fired; no gateway contacted)
Pending ──> Declined             (hard decline, or all routes exhausted)
Pending ──> Authorized ──> Captured ──> Refunded (roadmap)
```

## Idempotency

- The client sends an `Idempotency-Key` header.
- Same completed key + same payload → replay the stored result without another gateway call.
- Same key + different payload → `422`.
- The key has a **unique index in the database**. If two requests race past the application check, the database prevents a second payment row and the loser reloads the stored winner.
- **Known boundary:** gateway execution currently happens before the first key is persisted. The unique index protects database identity, not an external side effect that already happened. A real integration should reserve an in-progress record before calling the provider, pass the provider's own idempotency key and reconcile uncertain outcomes.

## Routing

Configuration-driven (`appsettings.json`): each gateway declares supported currencies, a max amount, and a priority. A payment's route = every eligible gateway, ordered by priority. Adding an acquirer = one gateway class + one config entry.

## Cascade policy

| Gateway result | Meaning | Action |
|---|---|---|
| `Approved` | Done | Authorize + capture, stop |
| `SoftDecline` | Recoverable (insufficient funds…) | Try next gateway |
| `Error` | Gateway problem, not card problem | Try next gateway |
| `HardDecline` | Fraud signal (stolen/blocked card) | **Stop immediately** — never shop a bad card to another acquirer |

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
| Unexpected | 500, generic message |

## Deliberate simplifications

In-process simulated gateways; stubbed card/IP countries; SQLite + `EnsureCreated` instead of migrations; auto-capture immediately after authorization.

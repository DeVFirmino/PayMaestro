# PayMaestro — Mini Payment Orchestration API

A .NET study project on **payment orchestration**: one API in front of many payment gateways, with smart routing, decline-aware cascades, completed-outcome replay, fraud screening and an audit-first design — inspired by how orchestration platforms serve the iGaming space.

## Why this exists

Payment orchestration is the layer *above* payment gateways: the merchant integrates once, many acquiring routes sit behind it, and the platform adds the intelligence — picking the best route per transaction, retrying safely when a route fails, and keeping evidence of everything. I built PayMaestro over a weekend, **spec-first**, to understand that domain hands-on. Every design decision is documented in [docs/SPEC.md](docs/SPEC.md).

## Related certifications

- [iGaming Academy — Anti-Fraud & Payments Handling (2026)](docs/certificates/Daniel_Silva_Anti_Fraud_and_Payments_Handling_2026.pdf)
- iGaming Academy — Anti-Money Laundering and Counter Terrorist Financing for Online Operators (2026)

## The pipeline

`request → completed-outcome replay → key reservation → fraud screening → route selection → decline-aware gateway cascade → persistence`

The reservation is committed **before** any gateway is contacted, so a duplicate request meets the key while the money movement is still ahead of it: a settled key replays its stored outcome, and a key still in flight is answered with `409` instead of a second charge.

## Payment lifecycle

Transitions are guarded **inside the aggregate** — an invalid transition throws a domain exception. There is no path back from `Captured`, and refunds only exist after capture (void vs refund). The lifecycle strip in the diagram above shows the full picture: `Pending → Processing → Authorized → Captured`, with `FraudRejected` and `Declined` as terminal branches and `RequiresReconciliation` as the holding state for a charge that never answered (`Captured → Refunded` is roadmap).

## Cascading in action

Card ending `1111` is soft-declined by AlphaPay — a *recoverable* failure — so the orchestrator cascades and BetaPay saves the sale: attempt 1 comes back `SoftDecline (51 insufficient funds)`, attempt 2 gets `Approved (00)`, and the merchant receives `200 Captured` with the full attempt trail. A hard decline (card `0000`) would stop the cascade immediately: retrying a stolen card on another acquirer is not resilience.

## Key engineering decisions

**Idempotency is reserve-first.** The payment row is inserted in `Processing` — claiming the key — before a gateway is contacted, and the unique index on that key decides the winner of a race. The loser has not charged anything yet: it either replays the winner's settled outcome or gets `409` while the winner is still in flight. Each attempt also carries a derived key (`{idempotency-key}:{gateway}:{attempt}`) so the provider's own deduplication recognises a retry. `IdempotencyReservationTests` drives two concurrent requests through a real SQLite database and asserts the gateway was charged exactly once.

**An unknown outcome is not a failure.** When a gateway stops answering, the payment moves to `RequiresReconciliation` and the cascade stops — charging the next acquirer while the first may hold the money is how a customer gets billed twice. `POST /api/payments/{id}/reconcile` asks the provider what happened to the key that attempt already used, and settles the payment from the answer instead of charging again.

**Hard vs soft declines drive the cascade policy.** Soft declines (insufficient funds, timeouts) and gateway errors cascade to the next route; hard declines (stolen or blocked cards) stop everything immediately.

**Fraud screening runs before any gateway is contacted.** Rules implement the `IFraudRule` Domain contract and every hit is stored as a `FraudFlag`. The first rule is live: **decline velocity** — a card with 3+ declined attempts in 24h is a card-testing pattern, so it is rejected with zero gateway calls.

**Routing is configuration, not code.** Gateway eligibility (supported currencies, amount caps) and priority live in `appsettings.json`, bound with the options pattern. Adding an acquirer is one class and one config entry — no business logic changes (open/closed principle).

**Rich domain model.** Private setters, factory creation and guarded transitions make invalid payment states unrepresentable by construction. My earlier projects used an anemic model; moving the invariants into the aggregate is a deliberate evolution.

**Audit-first persistence.** Every gateway attempt (gateway, order, result code, duration) and every fraud flag is persisted, and deletes are restricted at the database level — payment records are treated as regulatory evidence. If an acquirer issues an RFI, `GET /api/payments/{id}` returns the evidence pack. PCI-aware by design: only the BIN and last four digits are ever stored, never the full PAN.

**One class per reason to change.** `CreatePaymentUseCase` runs the pipeline; `GatewayRouter` picks the eligible gateways; `CascadeExecutor` owns the retry policy; each fraud rule and each gateway implements a single Domain contract (dependency inversion — the Domain has zero external references).

## Architecture

Clean Architecture, modular monolith: `API → Application → Domain ← Infrastructure`. The Domain holds entities, the state machine and all contracts (gateways, fraud rules, repositories with read/write segregation and a unit of work); the Infrastructure implements them (EF Core + SQLite, mock gateways); the Application holds the use cases, one class per operation.

The behaviour that matters is covered by xUnit tests — the cascade policy (approve, cascade, hard-stop) and the payment state machine:

```bash
dotnet test
```

## Run it

```bash
dotnet run --project src/PayMaestro.API
# the browser opens Swagger automatically — or go to http://localhost:5225/swagger
```

`POST /api/payments` with header `Idempotency-Key: <any-uuid>` and body:

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

| Card ending | Behaviour |
|---|---|
| `0000` | Hard decline everywhere — cascade stops, payment `Declined` |
| `1111` | Soft decline on AlphaPay — cascades to BetaPay |
| `2222` | Soft decline on BetaPay |
| `3333` | Gateway error on GammaPay |
| anything else | Approved on the first eligible gateway |

Also try: card `…9999`, which the mock acquirer settles and then fails to answer — the payment comes back `RequiresReconciliation` with no second gateway attempted, and `POST /api/payments/{id}/reconcile` turns it into `Captured` without another charge. Then: an amount above 5000 EUR (skips AlphaPay's cap — routing in action), a currency no gateway supports (declined with zero attempts), the same `Idempotency-Key` again after completion (identical stored response; the same key with a *different* amount returns `422`) — and the fraud rule: decline card `…0000` three times, and the **fourth** payment on that card comes back `FraudRejected` with an empty attempt list, because the velocity rule blocked it before any gateway was called.

## What's next

More fraud rules on the same `IFraudRule` contract, as specified in [docs/SPEC.md](docs/SPEC.md): geo mismatch (IP country vs card country — needs real BIN/GeoIP data) and amount anomaly. Then refunds, and CI/CD to Azure Container Apps — the same pipeline as my [Sports Betting API](https://github.com/DeVFirmino/SportsBetting), which runs there today.

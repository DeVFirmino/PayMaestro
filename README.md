# PayMaestro — Mini Payment Orchestration API

A .NET study project on **payment orchestration**: one API in front of many payment gateways, with smart routing, cascading retries, database-enforced idempotency, fraud screening and an audit-first design — inspired by how orchestration platforms serve the iGaming space.

> 🎓 The anti-fraud design follows the best practices of **Anti-Fraud & Payments Handling** taught by the **iGaming Academy** — see my [certificate](docs/certificates/Daniel_Silva_Anti_Fraud_and_Payments_Handling_2026.pdf).

## Why this exists

Payment orchestration is the layer *above* payment gateways: the merchant integrates once, many acquiring routes sit behind it, and the platform adds the intelligence — picking the best route per transaction, retrying safely when a route fails, and keeping evidence of everything. I built PayMaestro over a weekend, **spec-first**, to understand that domain hands-on. Every design decision is documented in [docs/SPEC.md](docs/SPEC.md).

**A note on how it was built:** I developed this project with AI assistance (Claude Code) as a pair programmer — it helped me scaffold, review and polish the implementation, the same way I'd use it on a real job. The spec, the domain decisions and the understanding behind them are mine, and I can walk through any part of the code and explain why it works the way it does.

## The pipeline

```mermaid
flowchart TD
    A["Merchant request + Idempotency-Key header"] --> B{"Key already seen?"}
    B -- "yes" --> R["Replay stored result — no double charge"]
    B -- "no" --> C["Create Payment (Pending)"]
    C --> K{"Fraud rules"}
    K -- "rule hit" --> L["FraudRejected — no gateway ever contacted"]
    K -- "clean" --> D["Router — eligibility + priority from config"]
    D --> E["Try next gateway in route"]
    E --> F{"Gateway result"}
    F -- "Approved" --> G["Authorized, then Captured"]
    F -- "HardDecline" --> H["Declined — never retry a stolen card"]
    F -- "SoftDecline / Error" --> J{"More gateways?"}
    J -- "yes" --> E
    J -- "no" --> H
    G --> I[("SQLite — payments, attempts, fraud flags")]
    H --> I
    L --> I
```

## Payment lifecycle

Transitions are guarded **inside the aggregate** — an invalid transition throws a domain exception. There is no path back from `Captured`, and refunds only exist after capture (void vs refund).

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> FraudRejected: fraud rule hit
    Pending --> Declined: hard decline / routes exhausted
    Pending --> Authorized: gateway approved
    Authorized --> Captured: auto capture
    Captured --> Refunded: roadmap
```

## Cascading in action

Card ending `1111` is soft-declined by AlphaPay — a *recoverable* failure — so the orchestrator cascades and BetaPay saves the sale. A hard decline (card `0000`) would stop the cascade immediately: retrying a stolen card on another acquirer is not resilience.

```mermaid
sequenceDiagram
    participant M as Merchant
    participant O as PayMaestro
    participant A as AlphaPay
    participant B as BetaPay
    M->>O: POST /api/payments (card ...1111)
    O->>A: process attempt 1
    A-->>O: SoftDecline (51 insufficient funds)
    Note over O: recoverable — cascade continues
    O->>B: process attempt 2
    B-->>O: Approved (00)
    Note over O: Authorized → Captured
    O-->>M: 200 Captured + full attempt trail
```

## Key engineering decisions

**Idempotency is enforced by the database, not just the code.** The idempotency key has a unique index, so two concurrent retries are serialised by the database itself — the losing insert is caught and the stored outcome is replayed. A check-then-insert in application code alone has a race window between the read and the write.

**Hard vs soft declines drive the cascade policy.** Soft declines (insufficient funds, timeouts) and gateway errors cascade to the next route; hard declines (stolen or blocked cards) stop everything immediately.

**Fraud screening runs before any gateway is contacted.** Rules implement the `IFraudRule` Domain contract and every hit is stored as a `FraudFlag`. The first rule is live: **decline velocity** — a card with 3+ declined attempts in 24h is the classic card-testing pattern, so it's rejected with zero gateway calls. The rule design is informed by the iGaming Academy **Anti-Fraud & Payments Handling** certification ([see certificate](docs/certificates/Daniel_Silva_Anti_Fraud_and_Payments_Handling_2026.pdf)).

**Routing is configuration, not code.** Gateway eligibility (supported currencies, amount caps) and priority live in `appsettings.json`, bound with the options pattern. Adding an acquirer is one class and one config entry — no business logic changes (open/closed principle).

**Rich domain model.** Private setters, factory creation and guarded transitions make invalid payment states unrepresentable by construction. My earlier projects used an anemic model; moving the invariants into the aggregate is a deliberate evolution.

**Audit-first persistence.** Every gateway attempt (gateway, order, result code, duration) and every fraud flag is persisted, and deletes are restricted at the database level — payment records are treated as regulatory evidence. If an acquirer issues an RFI, `GET /api/payments/{id}` returns the evidence pack. PCI-aware by design: only the BIN and last four digits are ever stored, never the full PAN.

**One class per reason to change.** The orchestrator coordinates the pipeline; `CascadeExecutor` owns the retry policy; each fraud rule and each gateway implements a single Domain contract (dependency inversion — the Domain has zero external references).

## Architecture

Clean Architecture, modular monolith: `API → Application → Domain ← Infrastructure`. The Domain holds entities, the state machine and all contracts (gateways, fraud rules, repositories with read/write/update segregation and a unit of work); the Infrastructure implements them (EF Core + SQLite, mock gateways); the Application orchestrates use cases.

The behaviour that matters is covered by xUnit tests — the cascade policy (approve, cascade, hard-stop) and the payment state machine:

```bash
dotnet test
```

## Run it

```bash
dotnet run --project src/PayMaestro.API
# open http://localhost:5225 — Swagger UI is served at the root
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

Also try: an amount above 5000 EUR (skips AlphaPay's cap — routing in action), a currency no gateway supports (declined with zero attempts), the same `Idempotency-Key` twice (identical response, no second charge; same key with a *different* amount returns `422`) — and the fraud rule: decline card `…0000` three times, and the **fourth** payment on that card comes back `FraudRejected` with an empty attempt list, because the velocity rule blocked it before any gateway was called.

## What's next

More fraud rules on the same `IFraudRule` contract, as specified in [docs/SPEC.md](docs/SPEC.md): geo mismatch (IP country vs card country — needs real BIN/GeoIP data) and amount anomaly. Then refunds, and CI/CD to Azure Container Apps — the same pipeline as my [Sports Betting API](https://github.com/DeVFirmino/SportsBetting), which runs there today.

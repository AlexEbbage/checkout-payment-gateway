# Production Readiness and Trade-offs

This implementation is deliberately scoped to the coding exercise. It is not intended to be a complete production payment gateway, but it does include the production concerns I think are most important for this flow: validation before submission, idempotency, safe card handling, explicit failure semantics, observability, and automated tests.

## What is included

- Request validation before calling the acquiring bank
- Atomic idempotency at the merchant API boundary
- Clear distinction between validation rejection, bank decline, and bank unavailability
- No CVV persistence
- Only the last four card digits stored and returned
- Structured logging with correlation IDs
- Request duration logging
- Basic domain metrics instrumentation
- Tracing hooks via `ActivitySource`
- Automated tests around validation, orchestration, bank-client behaviour, and idempotency

## What I intentionally left out

I have kept the infrastructure deliberately lightweight. The exercise does not need a real database, distributed cache, authentication service, dashboard stack, or background workers to demonstrate the core payment flow.

In a production system, the next areas I would add are:

- Durable payment storage
- Distributed idempotency storage
- Merchant authentication and authorization
- Merchant-scoped payment retrieval
- PCI-aligned tokenisation
- OpenTelemetry export to the platform observability stack
- Dashboards and alerting
- Provider reconciliation
- Operational runbooks
- Rate limiting and abuse protection

## Idempotency

The current implementation uses in-memory atomic reservation and completion. This prevents two concurrent requests with the same idempotency key from both calling the acquiring bank.

In production, idempotency would need to be backed by shared durable storage and scoped by merchant, for example:

```text
merchant_id + idempotency_key
```

That storage would also need an expiry policy, a clear state model, and recovery handling for requests that are left in progress because of a process crash or deployment.

## Downstream bank ambiguity

Gateway idempotency protects the merchant-facing API boundary. It does not guarantee that the acquiring bank deduplicates downstream requests.

If the gateway sends an authorization request and does not receive a response, the result is ambiguous. The bank may have processed the authorization, or it may not have received the request at all.

For that reason, I would not blindly retry authorization requests unless the provider contract makes that safe. In a production integration I would prefer to send a stable provider reference and rely on one or more of:

- Provider idempotency
- Status lookup
- Polling
- Webhooks
- Reconciliation reports
- Manual investigation tooling

The challenge simulator only exposes a simple `POST /payments` endpoint, so the implementation treats unavailable or timed-out bank calls as dependency failures and documents the production trade-off.

## Storage

The exercise uses in-memory payment storage because the challenge allows a test-double style repository and the focus is the payment flow rather than database plumbing.

The repository boundary is still explicit, so it could be replaced with SQL Server, PostgreSQL, DynamoDB, or another durable store without changing the controller or payment orchestration logic.

## Observability

The solution includes lightweight observability primitives rather than a full monitoring stack.

It emits structured logs, returns a correlation ID, records request duration, and instruments payment-specific metrics. In production I would export logs, metrics, and traces through the platform’s standard observability tooling, with alerts for bank unavailability, latency, error rate, and unusual rejection/decline patterns.

Sensitive values such as card number, CVV, authorization code, raw idempotency key, and correlation-specific identifiers should not be used as metric dimensions.

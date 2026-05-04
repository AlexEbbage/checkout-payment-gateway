# Payment Gateway Challenge (.NET)

This is my implementation of the Checkout.com payment gateway challenge.

The API supports two operations:

- `POST /api/payments` to process a card payment
- `GET /api/payments/{id}` to retrieve a stored payment summary

A valid payment request is sent to the supplied acquiring bank simulator. The result is stored and can be retrieved later with masked card details.

I kept the implementation deliberately small, but added a few things I would expect to think about in a payment flow: validation before bank submission, idempotency, clear handling of bank failures, safe card-data handling, logging, metrics, tracing hooks, and tests around the important behaviours.

## Requirements

- .NET 8 SDK
- Docker, for the acquiring bank simulator

Check the installed SDKs with:

```bash
dotnet --list-sdks
```

## Running locally

Start the bank simulator:

```bash
docker-compose up
```

Run the API:

```bash
dotnet run --project src/PaymentGateway.Api
```

Run the tests:

```bash
dotnet test
```

Swagger is available in Development at:

```text
https://localhost:{port}/swagger
```

The port is printed by `dotnet run`.

## API

### Process a payment

```http
POST /api/payments
```

Optional headers:

```http
Idempotency-Key: unique-request-key
X-Correlation-Id: caller-correlation-id
```

Example request:

```json
{
  "cardNumber": "4242424242424241",
  "expiryMonth": 12,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 1050,
  "cvv": "123"
}
```

Example response:

```json
{
  "id": "d3a9bd6f-f66f-4d35-b0db-413e684c5af1",
  "status": "Authorized",
  "lastFourCardDigits": "4241",
  "expiryMonth": 12,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 1050,
  "authorizationCode": "auth_123"
}
```

### Retrieve a payment

```http
GET /api/payments/{id}
```

Returns the stored payment summary. The full card number and CVV are never returned.

## Validation

Requests are validated before the acquiring bank is called.

| Field | Rule |
|---|---|
| Card number | Required, numeric, 14-19 digits |
| Expiry month | Required, 1-12 |
| Expiry date | Must be in the future |
| Currency | Required, supported 3-letter currency code |
| Amount | Positive integer in minor units |
| CVV | Required, numeric, 3-4 digits |

Supported currencies are `GBP`, `USD`, and `EUR`.

Invalid requests return `400 Bad Request` and are not sent to the bank simulator.

## Bank simulator

The supplied simulator responds based on the last card digit:

| Last digit | Result |
|---|---|
| Odd | Authorized |
| Even | Declined |
| 0 | 503 Service Unavailable |

A decline is treated as a valid bank response and is stored. A `503`, timeout, or unexpected bank response is treated as an upstream dependency failure.

## Idempotency

The API supports the `Idempotency-Key` header.

The idempotency check uses:

```text
Idempotency-Key + normalized request hash
```

The store uses an atomic reservation step before the bank is called:

```text
TryStartAsync(key, requestHash)
    +-- Started                  -> this request can call the bank
    +-- CompletedSamePayload      -> return the original response
    +-- InProgressSamePayload     -> return 425 Too Early
    +-- ConflictDifferentPayload  -> return 409 Conflict
```

This prevents two concurrent duplicate requests with the same key from both calling the acquiring bank.

For this challenge the idempotency store is in-memory. In production it would need to be backed by shared durable storage and scoped by merchant identity.

## Retry behaviour

The gateway does not automatically retry payment authorization calls to the bank.

That is intentional. Authorization is a side-effecting operation. If the bank receives the request but the gateway times out waiting for the response, blindly retrying could create a duplicate authorization unless the provider supports idempotency or a status lookup.

The simulator only exposes `POST /payments`, so unavailable or timed-out bank calls are returned as dependency failures. In a production integration I would use a stable provider reference and reconcile ambiguous states through provider lookup, polling, webhooks, or settlement reports.

## Card data and security

The implementation avoids storing or returning sensitive card data:

- the full card number is not returned
- CVV is not stored
- only the last four card digits are persisted and returned
- logs and metrics do not include PAN, CVV, raw idempotency keys, or authorization codes
- validation errors do not echo sensitive card data

Production hardening would include PCI-aligned tokenisation, encryption at rest, secret management, merchant authentication, merchant-scoped retrieval, audit logging, retention policies, and rate limiting.

## Observability

The implementation includes lightweight observability rather than a full monitoring stack:

- structured logs with `ILogger<T>`
- `X-Correlation-Id` support
- request logging for method, path, status code, and duration
- domain metrics via `System.Diagnostics.Metrics`
- tracing hooks via `ActivitySource`

The metrics and traces are instrumented in code without binding the application to a specific vendor. In production they would be exported through the platform’s OpenTelemetry setup or equivalent observability tooling.

## Testing

The tests cover the main behaviours:

- validation rules
- invalid requests not calling the bank
- authorized and declined bank responses
- payment persistence and retrieval
- bank unavailable handling
- bank client response mapping
- idempotency replay
- idempotency conflicts
- concurrent idempotency reservation

The tests are mostly unit-level and deterministic. For a production service I would add API integration tests using `WebApplicationFactory`, contract tests for the bank integration, and a small smoke test against the Docker simulator.

## Design trade-offs

This is a focused coding challenge implementation, not a complete payment platform.

The main trade-offs are:

- storage is in-memory because the challenge permits test-double persistence
- idempotency is atomic but in-memory, so it would need durable shared storage in production
- authentication and merchant scoping are not implemented
- full OpenTelemetry exporters, dashboards, and alerts are not included
- bank authorization calls are not automatically retried because they may have side effects
- the solution stays as one API project with clear folders instead of a larger multi-project structure

## Production hardening

The first things I would add for a real production service are:

1. durable payment storage
2. durable distributed idempotency storage
3. merchant authentication and merchant-scoped retrieval
4. provider reference/status lookup and reconciliation
5. PCI-compliant tokenisation
6. OpenTelemetry exporters, dashboards, and alerts
7. global exception handling middleware
8. health checks
9. rate limiting
10. deployment pipeline and runbooks

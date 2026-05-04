# Checkout.com Payment Gateway Challenge - .NET

This is a production-minded but intentionally lightweight implementation of the Checkout.com payment gateway challenge.

## Features

- `POST /api/payments` to process a payment
- `GET /api/payments/{id}` to retrieve a stored payment
- Request validation before acquiring bank calls
- Acquiring bank client using typed `HttpClient`
- Atomic idempotency using `Idempotency-Key`
- Structured logging and correlation IDs
- Lightweight metrics via `System.Diagnostics.Metrics`
- Tracing hooks via `ActivitySource`
- Safe card handling: only last four digits are stored/returned; CVV is not stored
- Unit tests for validation, service orchestration, idempotency, and bank client behaviour

## Design summary

The implementation is kept in one API project to avoid unnecessary ceremony for a small challenge, but it separates concerns clearly:

- Controller: HTTP concerns and status code mapping
- Payment service: application orchestration
- Validator: deterministic payment request validation
- Bank client: external acquiring bank integration
- Repository: payment persistence abstraction
- Idempotency store: atomic request reservation and response replay

## Idempotency

The gateway supports atomic idempotency using this flow:

```text
TryStartAsync(idempotencyKey, requestHash)
    -> Started: this request owns the key and may call the bank
    -> CompletedSamePayload: return original stored response
    -> InProgressSamePayload: return 425 Too Early
    -> ConflictDifferentPayload: return 409 Conflict
```

This prevents two concurrent requests with the same key from both calling the acquiring bank.

## Payment status semantics

- Rejected: invalid merchant request; bank is not called
- Authorized: acquiring bank approved the payment
- Declined: acquiring bank rejected the payment
- BankUnavailable: upstream dependency failure; this is not treated as declined

## Assumptions

- Supported currencies are GBP, USD and EUR.
- Amount must be a positive integer in minor units.
- Expiry date is valid until the end of the expiry month.
- Declined payments are stored because they are real payment attempts.
- Validation rejections are not stored because no payment was created.
- In-memory persistence is used because the challenge allows test-double storage.

## Production improvements

For a real production gateway, I would add:

- Durable payment storage
- Distributed/durable idempotency store
- Merchant authentication and payment ownership checks
- PCI-compliant tokenisation
- OpenTelemetry exporters
- Dashboards and alerting
- Provider reconciliation
- Secrets management
- Rate limiting
- Operational runbooks

## Run

Start the bank simulator:

```bash
docker-compose up
```

Run the API:

```bash
dotnet run --project src/PaymentGateway.Api
```

Run tests:

```bash
dotnet test
```

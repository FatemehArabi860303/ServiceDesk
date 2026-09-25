# ADR 0001: Use a transactional outbox for request-progress notifications

## Status

Accepted.

## Context

Customers need eventual notifications when important request progress occurs. Assignment and starting work are the initially supported progress events. Ticket operations must not fail or roll back when broker or email delivery is unavailable.

## Decision

The Functional Core produces one explicit pure `RequestProgressed` fact after an accepted ticket operation. The Shell passes that fact to Repository persistence. The Repository stores the ticket change, required ticket history, and an unpublished outbox message in one database transaction.

The target delivery architecture is:

```text
Outbox → RabbitMQ → Notification Consumer → Email
```

This step implements only the pure progress fact and transactional outbox. It does not implement RabbitMQ, a processor, email delivery, retries, leases, dead-letter queues, or notification-delivery tracking.

## Consequences

- An accepted assignment or start-work operation cannot commit without its outbox record.
- Broker and email failures cannot roll back successful ticket work.
- Future publishing is at-least-once and must use idempotency; exactly-once external email delivery is not assumed.
- Direct broker publication inside ticket persistence is prohibited.

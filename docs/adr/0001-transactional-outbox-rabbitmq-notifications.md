# ADR 0001: Use a transactional outbox for request-progress notifications

## Status

Accepted.

## Context

Customers need eventual notifications when important request progress occurs. Assignment and starting work are the initially supported progress events. Ticket operations must not fail or roll back when broker or email delivery is unavailable.

## Decision

The Functional Core produces one explicit pure `RequestProgressed` fact after an accepted ticket operation. The Shell passes that fact to Repository persistence. The Repository stores the ticket change, required ticket history, and an unpublished outbox message in one database transaction.

The implemented happy-path delivery architecture is:

```text
ServiceDesk Outbox → RabbitMQ → Notification Service → SMTP email
```

ServiceDesk converts its request-progress facts into generic `NotificationRequestedV1` messages before writing its transactional outbox. The Notification Service consumes the generic message and acknowledges RabbitMQ only after successful SMTP delivery. It does not understand ServiceDesk ticket or progress concepts.

Notification persistence, `(Producer, Id)` idempotency, duplicate suppression, retry scheduling, dead-letter handling, delivery lifecycle tracking, and multi-consumer concurrency guarantees remain deferred.

## Consequences

- An accepted assignment or start-work operation cannot commit without its outbox record.
- Broker and email failures cannot roll back successful ticket work.
- Delivery is not exactly-once. A durable idempotency design remains future work.
- Direct broker publication inside ticket persistence is prohibited.

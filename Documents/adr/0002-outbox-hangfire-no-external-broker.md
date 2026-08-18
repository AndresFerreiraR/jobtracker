# ADR-0002 — Outbox + Hangfire, No External Broker

- **Status:** Accepted
- **Date:** 2026-08-14
- **Deciders:** Team Lead
- **Consulted:** Full-stack team

## Context

Cross-module side-effects (invoice generation, customer notifications) must be:
- **Never lost** — even if the process crashes right after committing the business change.
- **At-least-once** delivered.
- Handled **asynchronously** so the HTTP response is fast.
- Runnable **locally without extra infra**.

Options considered:

| Option | Pros | Cons |
|---|---|---|
| Direct in-proc call inside the command handler | Trivial. | Couples modules; kills atomicity (business tx has to include the side effect); slow response. |
| Domain events via MediatR, dispatched pre-commit | Simple in-proc. | Not durable; a crash mid-flight loses side effects. |
| **Transactional Outbox in Postgres + Hangfire recurring worker** | Durable (business + outbox commit atomically). At-least-once. No new infra. | Custom code for interceptor + processor + DLQ. |
| Outbox + Kafka/RabbitMQ/ASB | Full pub/sub. | Extra infra to run + operate; overkill for MVP. |

## Decision

**Adopt Outbox + Hangfire.** An EF Core `SaveChangesInterceptor` collects domain events from aggregates, invokes MediatR handlers pre-save so integration event rows are inserted into `<module>.outbox_messages` in the same transaction. A Hangfire recurring job polls unprocessed rows using `FOR UPDATE SKIP LOCKED`, dispatches via a scoped `IIntegrationEventDispatcher`, marks processed, or increments attempts on failure. Poisoned rows (`attempts >= MaxAttempts`) are moved to a DLQ table.

## Consequences

**Positive**
- Zero-loss on producer side; at-least-once on consumer side.
- No new deployable infra (Postgres is already required).
- Hangfire dashboard gives operational visibility.
- Migration to a broker later is a **swap of the processor**, not a rewrite (see ADR-0001 migration path).

**Negative**
- Latency floor equal to the poll interval (10 s default). Acceptable — SLA is 15 s p95.
- Multi-worker deployments need `SKIP LOCKED` semantics (fine on Postgres 9.5+).
- Custom DLQ tooling.

## References

- 06-async-messaging.md
- ADR-0007 (domain → integration event mapping)

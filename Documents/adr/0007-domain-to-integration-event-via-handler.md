# ADR-0007 — Domain → Integration Event Mapping via Application Handler

- **Status:** Accepted
- **Date:** 2026-08-14
- **Deciders:** Team Lead

## Context

When an aggregate raises a domain event, we need to place a **serialized integration event** into the outbox. Two designs were possible:

### Option A — Persist the domain event directly

The interceptor serializes the raised `IDomainEvent` and inserts it into `outbox_messages`. Consumers deserialize back into the domain event type.

- Pros: minimal code, no mapping layer.
- Cons: **domain types become the wire format.** Renaming an internal field breaks consumers. Cross-module type sharing forces every consumer to reference `<Module>.Domain`, undoing the whole "bounded context" boundary. Versioning becomes painful.

### Option B — Application handler translates to a public `IIntegrationEvent`

The interceptor runs `IPublisher.Publish` pre-save. A registered `INotificationHandler<TDomainEvent>` in the module's Application layer constructs the corresponding `IIntegrationEvent` (defined in `<Module>.IntegrationEvents`) and calls `IOutboxWriter.EnqueueAsync(...)`. The public event is what gets serialized to the outbox and what other modules subscribe to.

- Pros: internal (`Domain`) and external (`IntegrationEvents`) shapes are decoupled and independently versionable. Consumers depend only on the small `IntegrationEvents` contract project — the true "Open Host Service" pattern.
- Cons: one small handler per event.

## Decision

**Adopt Option B.** Every domain event that must cross a module boundary has:

1. An **internal** record in `Jobs.Domain` implementing `IDomainEvent`.
2. A **public** record in `Jobs.IntegrationEvents` implementing `IIntegrationEvent`, with a `JobId`, `EventId` (stable, preserved across the pipeline), `OccurredOn`.
3. An `INotificationHandler<TDomainEvent>` in `Jobs.Application/EventHandlers/` that maps and calls `IOutboxWriter.EnqueueAsync`.

**Invariant:** `IntegrationEvent.EventId == DomainEvent.Id`. This is the stable idempotency key used by `processed_inbox` on consumers.

## Consequences

**Positive**
- Domain refactors do not break consumers.
- Consumers reference `<Module>.IntegrationEvents` (records only, no framework deps) — genuinely a public contract project.
- Event versioning is explicit: add `V2` records side by side.

**Negative**
- Duplication for events that are near-identical inside/outside. Acceptable; the duplication is deliberate.

## References

- 01-domain-model.md §8
- 06-async-messaging.md §3
- ADR-0002 (Outbox + Hangfire)

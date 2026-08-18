# ADR-0001 — Modular Monolith over Microservices

- **Status:** Accepted
- **Date:** 2026-08-14
- **Deciders:** Team Lead
- **Consulted:** Full-stack team, Architecture

## Context

JobTracker starts with a small team, a single tenant onboarding stream, and no operational team. We need a shape that:
1. Gives us DDD-style bounded contexts (Jobs, Billing, Notifications).
2. Doesn't tax us with distributed-systems ops (network partitions, distributed traces, service discovery, per-service DBs) before we have real load.
3. Leaves a **credible migration path** to microservices if we grow.

We considered three options.

## Options

| Option | Pros | Cons |
|---|---|---|
| **Monolith (single project, single DB, shared model)** | Simplest. | Modules end up entangled; refactoring later is expensive. |
| **Modular Monolith (bounded contexts as modules, integration events over outbox, schema per module)** | DDD boundaries enforced; single deployable; single DB reduces ops; contracts modelled as if remote → future-proof. | Slight up-front discipline overhead. |
| **Microservices from day one (Jobs, Billing, Notifications as separate services + broker)** | Independent scaling and deployments. | Ops cost 3–5×; distributed transactions; overkill for current load; observability requires infra we don't have. |

## Decision

**Adopt a Modular Monolith.** Each bounded context lives in its own set of `.csproj` (Domain, Application, Infrastructure, Presentation, IntegrationEvents) inside `src/Modules/<Module>/`. Modules communicate **only** via outbox + Hangfire dispatch, never via direct references to other modules' Domain/Infrastructure. Each module owns a Postgres schema.

## Consequences

**Positive**
- One deployable, one connection string, one CI pipeline per tier → productivity high.
- Bounded contexts and public contracts (`*.IntegrationEvents`) are already modelled the way they'd need to be if extracted.
- NetArchTest enforces the boundaries at build time.

**Negative**
- Requires discipline: no shortcuts of directly using another module's DbContext.
- All modules ship together on every release (blast radius = whole app).
- One process → one language runtime (fine for now).

**Migration path**
- To extract a module: move its projects to a new solution, replace the local outbox dispatcher with a broker publisher, keep the same `IIntegrationEvent` contracts. Consumers on the other side keep the same idempotent handlers.

## References

- 00-architecture-overview.md
- 03-backend-solution.md
- 06-async-messaging.md §11 (migration path)

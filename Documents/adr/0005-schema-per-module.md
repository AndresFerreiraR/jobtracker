# ADR-0005 — Schema per Module in a Single Postgres DB

- **Status:** Accepted
- **Date:** 2026-08-14
- **Deciders:** Team Lead

## Context

We have a single Postgres database (see ADR-0004). We still want strong logical isolation between modules (Jobs, Billing, Notifications, Identity) so that:
- Migrations run per module without stepping on each other.
- Foreign keys never leak across bounded contexts.
- Access control can be tightened per-schema later.
- A future extraction of a module to its own service is trivial: `pg_dump --schema=jobs`.

## Decision

**One Postgres schema per module.** Each module owns:
- Its own tables (`jobs.jobs`, `billing.invoices`, …).
- Its own `outbox_messages` and `processed_inbox` tables.
- Its own EF Core `DbContext` with `HasDefaultSchema("<module>")`.
- Its own EF migrations under `Modules/<Module>.Infrastructure/Persistence/Migrations/` and its own `__ef_migrations_history` table inside its schema.

**Cross-schema foreign keys are forbidden.** Cross-context references are stored as raw `UUID` values only (`assignee_id`, `customer_id`). Referential correctness is enforced at the application boundary + integration events.

## Consequences

**Positive**
- Encapsulation matches code architecture.
- Migrations can be developed / applied per module in isolation.
- `pg_dump -n jobs` extracts the module.
- Access policies (`GRANT SELECT ON ALL TABLES IN SCHEMA billing TO reporting`) become natural.

**Negative**
- Cross-context joins are impossible in SQL. Data composition happens at the API layer (BFF composition) or via denormalization driven by integration events. This is a **feature**, not a bug — it forces us to model contracts.
- Slightly more setup ceremony (schemas + history tables).

## References

- 02-database-design.md §2 & §7
- ADR-0001 (modular monolith)
- ADR-0004 (multi-tenancy)

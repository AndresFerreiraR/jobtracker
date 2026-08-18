# ADR-0004 — Multi-Tenancy: Shared DB, Discriminator Column

- **Status:** Accepted
- **Date:** 2026-08-14
- **Deciders:** Team Lead

## Context

JobTracker is multi-tenant SaaS. Every table's rows belong to exactly one `Organization`. Options:

| Option | Isolation | Onboarding | Migration cost | Ops |
|---|---|---|---|---|
| **Database per tenant** | Strong (physical). | Provision a DB. | High — apply migrations to N DBs. | Connection-pool exhaustion, N backups. |
| **Schema per tenant** | Medium. | Create schema. | High (same as above). | Explosion of DDL objects. |
| **Shared DB, shared tables, `organization_id` discriminator** | Logical only. | Insert row in `organizations`. | O(1) migrations. | Cheap. |

## Decision

**Adopt shared-DB, discriminator column.** Every non-shared table has an `organization_id UUID NOT NULL`; every composite index leads with it. Enforcement layers:
1. `ITenantContext` populated from the JWT `org` claim at request time.
2. EF Core **global query filter**: `entity => entity.OrganizationId == TenantAccessor.CurrentOrgId`.
3. All writes assign `OrganizationId` from `ITenantContext`, never from client input.
4. NetArchTest: every AggregateRoot MUST expose `OrganizationId`.
5. Postgres `CHECK` constraints and unique keys are tenant-scoped where relevant (e.g., `UNIQUE (organization_id, job_id)` in Billing).

## Consequences

**Positive**
- Trivial tenant onboarding (insert one row).
- Single connection pool; single migration set.
- All indexes and queries composed to lead with `organization_id`.

**Negative**
- Isolation is logical, not physical. A code bug could theoretically leak data → mitigated by the enforcement layers, PR review, and integration tests that assert cross-tenant queries return zero rows.
- Row-Level Security (Postgres RLS) is a **future defense-in-depth** hardening — we deliberately do NOT enable it now because our EF query filter already delivers the same effect and RLS adds operational complexity.

## References

- 02-database-design.md §1 & §10
- 00-architecture-overview.md §10

# ADR-0006 — `bigserial` PK on `outbox_messages` (Exception to "UUID everywhere")

- **Status:** Accepted
- **Date:** 2026-08-14
- **Deciders:** Team Lead

## Context

Our default rule (see 02-database-design.md §1) is UUID primary keys everywhere — for distributed generation, opacity, and consistency. `outbox_messages` is a candidate exception because:

- The worker's hot query is `WHERE processed_on IS NULL ORDER BY id LIMIT N`.
- Insert order matches processing order. Inserts happen only at commit time from the API tier.
- The table is **module-internal** — never exposed to clients or other modules; only the module's Hangfire worker touches it.
- A partial index `WHERE processed_on IS NULL` is the actual working set.

Options weighed:

| PK type | Insert perf | Hot query perf | Uniformity |
|---|---|---|---|
| `bigserial` | Sequential, no page splits. | Rightmost B-tree leaf, tiny I/O. | Breaks the "UUID everywhere" rule. |
| `uuid v4` (random) | Random inserts → page splits under load. | Larger index, more I/O. | Uniform. |
| `uuid v7` (time-ordered, app-generated) | Similar to bigserial. | Similar. | Uniform. Not native in PG16 — must generate app-side (`UuidGenerator.CreateVersion7()` in .NET 9). |

## Decision

**Use `bigserial` for `outbox_messages.id`.** The **logical** event identifier remains the `event_id UUID` column, which is the value that flows to consumers as the idempotency key. `id` is purely a physical DB detail (ordering + primary key).

## Consequences

**Positive**
- Zero fragmentation on the hot table; partial index stays compact.
- Worker `SELECT ... ORDER BY id` uses a monotonically increasing rightmost scan → fast even at millions of rows.
- Simple mental model for operations (`id` is a debug-friendly bigint).

**Negative**
- Inconsistent with "UUID everywhere" — documented explicitly in the schema section.
- Would need re-evaluation if we ever expose `outbox_messages` externally (we won't).

**Re-evaluation trigger**
- If PG18 uuid v7 becomes the default (native generator + fully time-ordered), we may switch for uniformity without perf loss. Nothing else needs to change downstream since `event_id` is the logical key.

## References

- 02-database-design.md §3.3
- 06-async-messaging.md §4

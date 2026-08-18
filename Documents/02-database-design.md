# 02 — Database Design (PostgreSQL 16)

> Scope: `jobs` schema (deep). `billing`, `notifications`, `identity` schemas outlined as stubs.
> Deliverables: SQL DDL (source of truth), EF Core Fluent configuration (matches DDL), initial migration plan, and the optimized cursor-paginated FTS query.

---

## 1. Design principles

| Principle | Applied |
|---|---|
| **Schema per module** | Physical isolation. Each module has its own EF DbContext + migrations history table. |
| **UUIDs everywhere** | `uuid` PKs (v7 when available in producer code) — no auto-increment, easier for distributed writes. |
| **Multi-tenant by discriminator** | Every table carries `organization_id` in the leading position of indexes. |
| **snake_case** | Postgres-idiomatic; EF Core translates via `UseSnakeCaseNamingConvention()` (EFCore.NamingConventions). |
| **Enums as text** | Human-readable in DB dumps; forward-compatible; no dependency on ordinal. |
| **Owned Value Objects flattened** | `Address` columns live inline in `jobs`. No junk join. |
| **Soft deletes: no** | Business model uses `Cancelled` status; no `deleted_at` column on `jobs`. |
| **Outbox per schema** | Each module has its own `outbox_messages` table (data ownership stays with the module). |
| **Optimistic concurrency** | `xmin::text::bigint` mapped as `uint` "version" in EF; no separate `row_version` column needed. |
| **Timestamps** | `timestamptz` for wall-clock, `now()` server-side for `created_at`/`updated_at`. |

---

## 2. Schemas

```sql
CREATE SCHEMA IF NOT EXISTS jobs;
CREATE SCHEMA IF NOT EXISTS billing;
CREATE SCHEMA IF NOT EXISTS notifications;
CREATE SCHEMA IF NOT EXISTS identity;

-- Extensions (idempotent)
CREATE EXTENSION IF NOT EXISTS "pgcrypto";       -- gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS "btree_gin";      -- composite GIN for tenant + fts
```

---

## 3. `jobs` schema — DDL

### 3.1 `jobs.jobs`

```sql
CREATE TABLE jobs.jobs (
    id                   uuid           PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id      uuid           NOT NULL,
    title                varchar(200)   NOT NULL,
    description          varchar(4000)  NOT NULL DEFAULT '',
    status               varchar(20)    NOT NULL
        CHECK (status IN ('Draft','Scheduled','InProgress','Completed','Cancelled')),

    -- Address (owned value object, flattened)
    address_street       varchar(200)   NOT NULL,
    address_city         varchar(120)   NOT NULL,
    address_state        varchar(60)    NOT NULL,
    address_zip_code     varchar(10)    NOT NULL,
    address_latitude     numeric(9,6)   NULL,
    address_longitude    numeric(9,6)   NULL,

    scheduled_date       timestamptz    NULL,
    started_at           timestamptz    NULL,
    completed_at         timestamptz    NULL,
    cancelled_at         timestamptz    NULL,
    cancellation_reason  varchar(500)   NULL,
    signature_url        varchar(1000)  NULL,

    assignee_id          uuid           NULL,
    customer_id          uuid           NOT NULL,

    created_at           timestamptz    NOT NULL DEFAULT now(),
    updated_at           timestamptz    NOT NULL DEFAULT now(),

    -- Full-text search vector, maintained by trigger (see §5)
    search_vector        tsvector       GENERATED ALWAYS AS (
        setweight(to_tsvector('english', coalesce(title, '')), 'A') ||
        setweight(to_tsvector('english', coalesce(description, '')), 'B')
    ) STORED,

    CONSTRAINT chk_jobs_terminal_dates CHECK (
        (status <> 'Completed' OR (completed_at IS NOT NULL AND signature_url IS NOT NULL))
        AND (status <> 'Cancelled' OR (cancelled_at IS NOT NULL AND cancellation_reason IS NOT NULL))
        AND (status <> 'InProgress' OR started_at IS NOT NULL)
        AND (status <> 'Scheduled' OR (scheduled_date IS NOT NULL AND assignee_id IS NOT NULL))
    )
);
```

**Notes:**
- `search_vector` is a **generated column** (Postgres 12+). No trigger needed; Postgres recomputes on write.
- The `CHECK` at the bottom is a *belt-and-suspenders* server-side invariant that mirrors what the aggregate enforces in code.
- No FKs to `identity.users` or Contacts — cross-schema FKs are forbidden by design (see 00 §3, golden rule 4). Referential integrity is enforced by the application layer + integration events on tenant-scoped ownership.

### 3.2 `jobs.job_photos`

```sql
CREATE TABLE jobs.job_photos (
    id            uuid           PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id        uuid           NOT NULL
        REFERENCES jobs.jobs(id) ON DELETE CASCADE,
    url           varchar(1000)  NOT NULL,
    captured_at   timestamptz    NOT NULL,
    caption       varchar(500)   NULL,
    created_at    timestamptz    NOT NULL DEFAULT now()
);
```

`ON DELETE CASCADE`: safe because `JobPhoto` is inside the `Job` aggregate — its lifecycle is bound to the root.

### 3.3 `jobs.outbox_messages`

```sql
CREATE TABLE jobs.outbox_messages (
    id                bigserial      PRIMARY KEY,
    event_id          uuid           NOT NULL UNIQUE,
    type              varchar(500)   NOT NULL,             -- FQN of integration event
    content           jsonb          NOT NULL,             -- serialized event
    occurred_on       timestamptz    NOT NULL DEFAULT now(),
    processed_on      timestamptz    NULL,
    attempts          smallint       NOT NULL DEFAULT 0,
    last_error        text           NULL,
    organization_id   uuid           NOT NULL              -- for tenant-scoped diagnostics
);
```

**Design notes:**
- `bigserial` (not uuid) — **intentional exception** to the "UUID everywhere" rule. The outbox is an operational, module-internal table (never exposed to clients or other modules). Its hot query is `WHERE processed_on IS NULL ORDER BY id LIMIT N`, which benefits enormously from a tight, monotonically-increasing 8-byte PK: the B-tree has zero fragmentation (all inserts go to the rightmost leaf), the partial index `ix_outbox_unprocessed` stays compact, and worker scans are effectively sequential I/O. A random UUID v4 would double the PK width and randomize insert positions, causing page splits under load. UUID v7 (time-ordered) would be a valid alternative but Postgres 16 has no native generator (would need app-side generation in .NET 9 via `UuidGenerator.CreateVersion7()`). We favour `bigserial` here for simplicity + performance.
- `event_id` (the uuid column) is the **stable identifier** used by consumers for idempotency — never `id`. `id` is a physical DB detail; `event_id` is the logical event identity.
- `content` is `jsonb` → indexable, queryable, cheap.
- `attempts` + `last_error` support exponential backoff and DLQ decisions in Hangfire jobs.

### 3.4 `jobs.processed_inbox` (idempotency on consumers *inside* this module)

Not needed inside `jobs` in this iteration (Jobs is only a producer). Billing and Notifications get their own `processed_inbox` tables (see §7 stubs).

---

## 4. Indexes on `jobs.jobs`

Every index leads with `organization_id` (multi-tenant discriminator).

```sql
-- 1) Full-text search per tenant, blended with common filters.
--    btree_gin extension enables composite (uuid, tsvector) GIN.
CREATE INDEX ix_jobs_org_search
    ON jobs.jobs USING GIN (organization_id, search_vector);

-- 2) Status filtering per tenant (partial for the hot path).
CREATE INDEX ix_jobs_org_status_scheduled
    ON jobs.jobs (organization_id, status, scheduled_date DESC);

-- 3) Cursor pagination key: (organization_id, created_at DESC, id) for stable ordering.
CREATE INDEX ix_jobs_org_created_id
    ON jobs.jobs (organization_id, created_at DESC, id);

-- 4) Assignee-oriented lookups (dashboards, "my jobs").
CREATE INDEX ix_jobs_org_assignee
    ON jobs.jobs (organization_id, assignee_id)
    WHERE assignee_id IS NOT NULL;

-- 5) Customer-oriented lookups.
CREATE INDEX ix_jobs_org_customer
    ON jobs.jobs (organization_id, customer_id);

-- 6) Date-range queries on scheduled_date per tenant.
CREATE INDEX ix_jobs_org_scheduled_date
    ON jobs.jobs (organization_id, scheduled_date)
    WHERE scheduled_date IS NOT NULL;
```

### 4.1 Indexes on `jobs.job_photos`

```sql
CREATE INDEX ix_job_photos_job_id ON jobs.job_photos (job_id);
```

Photos are loaded ONLY through the aggregate root; the FK index is sufficient.

### 4.2 Indexes on `jobs.outbox_messages`

```sql
-- Hot path: worker query "unprocessed, oldest first".
-- Partial index keeps it tiny even when the table is huge.
CREATE INDEX ix_outbox_unprocessed
    ON jobs.outbox_messages (id)
    WHERE processed_on IS NULL;
```

---

## 5. Optimized query — cursor-paginated FTS with photo count

**Requirements (from the assessment):**
- Full-text search on `title + description`.
- Filter by multiple statuses.
- Filter by date range.
- Cursor-based pagination (**NOT** OFFSET).
- Include photo count per job.
- Multi-tenant.

### 5.1 The query

```sql
-- Parameters:
--   @org         uuid                 -- tenant
--   @q           text                 -- optional websearch query
--   @statuses    varchar[]            -- optional array of statuses
--   @from        timestamptz          -- optional lower bound on scheduled_date
--   @to          timestamptz          -- optional upper bound on scheduled_date
--   @cursor_created_at  timestamptz   -- from previous page (nullable = first page)
--   @cursor_id          uuid          -- tiebreaker
--   @page_size          int           -- e.g. 20

SELECT
    j.id,
    j.title,
    j.description,
    j.status,
    j.scheduled_date,
    j.started_at,
    j.completed_at,
    j.assignee_id,
    j.customer_id,
    j.created_at,
    j.updated_at,
    -- Photo count via a correlated aggregate; index ix_job_photos_job_id supports it.
    (SELECT count(*) FROM jobs.job_photos p WHERE p.job_id = j.id) AS photo_count,
    -- Rank only if a query was provided (kept out of ORDER BY on empty query).
    CASE WHEN @q IS NOT NULL AND @q <> ''
         THEN ts_rank_cd(j.search_vector, websearch_to_tsquery('english', @q))
         ELSE NULL END AS rank
FROM jobs.jobs j
WHERE j.organization_id = @org
  AND (@q IS NULL OR @q = '' OR j.search_vector @@ websearch_to_tsquery('english', @q))
  AND (@statuses IS NULL OR j.status = ANY(@statuses))
  AND (@from IS NULL OR j.scheduled_date >= @from)
  AND (@to   IS NULL OR j.scheduled_date <= @to)
  -- Keyset pagination on (created_at DESC, id) — stable and non-overlapping.
  AND (@cursor_created_at IS NULL
       OR (j.created_at, j.id) < (@cursor_created_at, @cursor_id))
ORDER BY j.created_at DESC, j.id DESC
LIMIT @page_size + 1;   -- +1 to know if there is a next page
```

### 5.2 Building the next cursor

The application takes the returned rows:
- If it received `@page_size + 1` rows, the last one is dropped and its `(created_at, id)` becomes the `nextCursor` returned to the client (base64-encoded opaque string).
- If it received `≤ @page_size` rows, there is no next page.

The cursor format is `base64("<createdAtIso>|<uuid>")` in the API.

### 5.3 Why cursor over `OFFSET`

| Concern | `OFFSET N LIMIT M` | Keyset / cursor |
|---|---|---|
| Cost with large offsets | O(N+M) scan; page 500 is 500× slower than page 1. | O(M); each page is identical cost. |
| Consistency under writes | Rows can be skipped or duplicated when new rows are inserted between requests. | Stable: the cursor anchors on real row identity. |
| Index usage | Cannot fully use composite indexes; still scans N rows. | Uses `ix_jobs_org_created_id` end-to-end. |
| Deep pagination | Practically unusable past a few thousand rows. | Works to arbitrary depth. |

Trade-offs of keyset:
- Cannot "jump to page 47". Only next/prev.
- Requires a stable, unique tie-breaker (`id` after `created_at`).
- Cursor is opaque and versioned to allow schema changes.

### 5.4 EXPLAIN sketch (expected shape)

```
Limit
  ->  Index Scan using ix_jobs_org_created_id on jobs
        Index Cond: (organization_id = @org AND (created_at, id) < (@cursor_created_at, @cursor_id))
        Filter: (status = ANY(@statuses)) AND (search_vector @@ websearch_to_tsquery(...))
        -- FTS filter is applied after index scan when @q is provided;
        -- Postgres can prefer ix_jobs_org_search (GIN) when selectivity is high.
```

The planner picks between `ix_jobs_org_created_id` (order) and `ix_jobs_org_search` (FTS selectivity). `ANALYZE` after seeding is essential. If FTS is expected to be always-on, we can add:

```sql
CREATE INDEX ix_jobs_org_created_id_include_status
    ON jobs.jobs (organization_id, created_at DESC, id) INCLUDE (status);
```

(covering index → status filter satisfied from the index).

---

## 6. EF Core configuration (Fluent)

### 6.1 Startup wiring

```csharp
services.AddDbContext<JobsDbContext>((sp, opt) =>
{
    opt.UseNpgsql(cfg.GetConnectionString("JobTracker"), b =>
    {
        b.MigrationsHistoryTable("__ef_migrations_history", schema: "jobs");
        b.MigrationsAssembly(typeof(JobsDbContext).Assembly.FullName);
    });
    opt.UseSnakeCaseNamingConvention();
    opt.AddInterceptors(sp.GetRequiredService<InsertOutboxMessagesInterceptor>());
});
```

### 6.2 `JobsDbContext`

```csharp
internal sealed class JobsDbContext(
    DbContextOptions<JobsDbContext> options,
    ITenantContext tenant) : DbContext(options), IUnitOfWork
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("jobs");
        mb.ApplyConfigurationsFromAssembly(typeof(JobsDbContext).Assembly);
    }

    // Global tenant filter is applied per-entity in the configurations below,
    // reading tenant.OrganizationId at runtime.
}
```

### 6.3 `JobConfiguration`

```csharp
internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> b)
    {
        b.ToTable("jobs");
        b.HasKey(j => j.Id);

        b.Property(j => j.Id)
         .HasConversion(id => id.Value, v => new JobId(v));

        b.Property(j => j.OrganizationId)
         .HasConversion(id => id.Value, v => new OrganizationId(v));

        b.Property(j => j.Title).HasMaxLength(200).IsRequired();
        b.Property(j => j.Description).HasMaxLength(4000).IsRequired();
        b.Property(j => j.Status)
         .HasConversion<string>()
         .HasMaxLength(20)
         .IsRequired();

        b.OwnsOne(j => j.Address, a =>
        {
            a.Property(x => x.Street).HasColumnName("address_street").HasMaxLength(200).IsRequired();
            a.Property(x => x.City).HasColumnName("address_city").HasMaxLength(120).IsRequired();
            a.Property(x => x.State).HasColumnName("address_state").HasMaxLength(60).IsRequired();
            a.Property(x => x.ZipCode).HasColumnName("address_zip_code").HasMaxLength(10).IsRequired();
            a.Property(x => x.Latitude).HasColumnName("address_latitude").HasPrecision(9, 6);
            a.Property(x => x.Longitude).HasColumnName("address_longitude").HasPrecision(9, 6);
        });

        b.Property(j => j.AssigneeId)
         .HasConversion(id => id!.Value.Value, v => new AssigneeId(v));
        b.Property(j => j.CustomerId)
         .HasConversion(id => id.Value, v => new CustomerId(v));

        b.Property(j => j.CreatedAt);
        b.Property(j => j.UpdatedAt);

        // Optimistic concurrency mapped to xmin.
        b.Property(j => j.Version)
         .HasColumnName("xmin")
         .HasColumnType("xid")
         .ValueGeneratedOnAddOrUpdate()
         .IsConcurrencyToken();

        // Aggregate collection (never accessed outside root).
        b.HasMany(j => j.Photos)
         .WithOne()
         .HasForeignKey(p => p.JobId)
         .OnDelete(DeleteBehavior.Cascade);

        b.Navigation(j => j.Photos)
         .UsePropertyAccessMode(PropertyAccessMode.Field)
         .HasField("_photos");

        // Multi-tenant global query filter.
        b.HasQueryFilter(j => j.OrganizationId == TenantAccessor.CurrentOrgId);
        // TenantAccessor is a static holder wrapping ITenantContext (needed
        // because HasQueryFilter cannot capture DI directly).
    }
}
```

### 6.4 `JobPhotoConfiguration`

```csharp
internal sealed class JobPhotoConfiguration : IEntityTypeConfiguration<JobPhoto>
{
    public void Configure(EntityTypeBuilder<JobPhoto> b)
    {
        b.ToTable("job_photos");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasConversion(id => id.Value, v => new JobPhotoId(v));
        b.Property(p => p.JobId).HasConversion(id => id.Value, v => new JobId(v));
        b.Property(p => p.Url).HasMaxLength(1000).IsRequired();
        b.Property(p => p.Caption).HasMaxLength(500);
        b.Property(p => p.CapturedAt);
    }
}
```

### 6.5 `OutboxMessageConfiguration`

```csharp
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(o => o.Id);
        b.Property(o => o.EventId).IsRequired();
        b.HasIndex(o => o.EventId).IsUnique();
        b.Property(o => o.Type).HasMaxLength(500).IsRequired();
        b.Property(o => o.Content).HasColumnType("jsonb").IsRequired();
        b.Property(o => o.OccurredOn).IsRequired();
        b.Property(o => o.ProcessedOn);
        b.Property(o => o.Attempts);
        b.Property(o => o.LastError);
        b.Property(o => o.OrganizationId).IsRequired();
    }
}
```

### 6.6 `InsertOutboxMessagesInterceptor` (headline)

```csharp
public sealed class InsertOutboxMessagesInterceptor(IDateTimeProvider clock)
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData ed, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var ctx = ed.Context!;
        var domainEvents = ctx.ChangeTracker
            .Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.DrainEvents())
            .ToList();

        foreach (var evt in domainEvents)
        {
            ctx.Add(OutboxMessage.From(evt, clock.UtcNow));
        }
        return base.SavingChangesAsync(ed, result, ct);
    }
}
```

Rationale in 06-async-messaging.md; here we only wire it.

### 6.7 Read-optimized repository (projection to DTO)

`SearchAsync` uses raw SQL via `FromSql` + `AsNoTracking` because:
- `ts_rank_cd`, `websearch_to_tsquery`, and the `(created_at, id) < (@c, @i)` tuple comparison are not naturally expressible in LINQ.
- Cursor pagination benefits from a hand-tuned SQL that we can `EXPLAIN` deterministically.
- We project **directly to a DTO** (`JobListItemResponse`), skipping change tracking entirely.

```csharp
public async Task<PagedList<JobListItemResponse>> SearchAsync(
    JobSearchCriteria c, CancellationToken ct)
{
    var sql = FormattableStringFactory.Create(SearchSql,
        c.OrgId.Value, c.Query, c.Statuses?.Select(s => s.ToString()).ToArray(),
        c.From, c.To, c.Cursor?.CreatedAt, c.Cursor?.Id, c.PageSize);

    var rows = await _db.Database
        .SqlQuery<JobListItemRow>(sql)
        .AsNoTracking()
        .ToListAsync(ct);

    return PagedList.FromKeyset(rows, c.PageSize, r => new Cursor(r.CreatedAt, r.Id));
}
```

The exact `SearchSql` constant is the parametrized query from §5.1. Parameter binding uses `NpgsqlParameter` with typed arrays for `@statuses`.

---

## 7. `billing` and `notifications` schemas (stubs)

### 7.1 `billing`

```sql
CREATE TABLE billing.invoices (
    id                uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid          NOT NULL,
    job_id            uuid          NOT NULL,
    customer_id       uuid          NOT NULL,
    amount            numeric(12,2) NOT NULL,
    currency          char(3)       NOT NULL DEFAULT 'USD',
    status            varchar(20)   NOT NULL DEFAULT 'Draft',
    generated_at      timestamptz   NOT NULL DEFAULT now(),
    UNIQUE (organization_id, job_id)   -- one invoice per job per tenant
);

CREATE TABLE billing.processed_inbox (
    event_id     uuid          PRIMARY KEY,
    handler      varchar(200)  NOT NULL,
    processed_on timestamptz   NOT NULL DEFAULT now(),
    UNIQUE (event_id, handler)
);

CREATE TABLE billing.outbox_messages (LIKE jobs.outbox_messages INCLUDING ALL);
```

**Idempotency:** the invoice handler `INSERT`s into `processed_inbox` first (unique key `(event_id, handler)`). If the insert conflicts, the event was already processed → return.

### 7.2 `notifications`

```sql
CREATE TABLE notifications.notification_log (
    id                uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid          NOT NULL,
    recipient         varchar(320)  NOT NULL,
    channel           varchar(20)   NOT NULL,       -- 'Email'
    template          varchar(100)  NOT NULL,
    status            varchar(20)   NOT NULL,       -- 'Pending' | 'Sent' | 'Failed'
    attempts          smallint      NOT NULL DEFAULT 0,
    last_error        text          NULL,
    created_at        timestamptz   NOT NULL DEFAULT now(),
    sent_at           timestamptz   NULL
);

CREATE TABLE notifications.processed_inbox (LIKE billing.processed_inbox INCLUDING ALL);
```

---

## 8. Normalization vs denormalization — analysis

### 8.1 What we normalize
- `Address` is inlined as owned columns on `jobs.jobs`. That is technically denormalization (would be a separate `addresses` table in 3NF), but the domain treats Address as a Value Object with no independent lifecycle → the inlined columns are the correct model. Structural equality lives in code, not DB.
- `job_photos` is a **1-to-many** child; kept in its own table.
- Foreign identifiers (`customer_id`, `assignee_id`) are stored as UUIDs; the *names* are not stored.

### 8.2 When we would denormalize (and why)

**Case A: `customer_name` snapshot on `jobs.jobs`.**
Justification: the Jobs list is a hot read path. Joining across schemas is forbidden (see architecture). Fetching the customer per-row via API from a Contacts service is O(N) network calls per page.

**Cost of denormalization:**
- Extra column `customer_name_snapshot varchar(200)`, refreshed on `CustomerRenamedIntegrationEvent`.
- Eventual consistency window: readers can see stale names until the event is processed. Acceptable for a "job list".

**Case B: `photo_count` snapshot.**
Rejected in this iteration. `(SELECT count(*) ...)` correlated subquery is fine with the FK index. Would revisit if the table grows past ~10M rows per tenant.

**Case C: read model / CQRS split.**
Deferred. Would materialize a `job_list_v` view/table populated from integration events for hyper-scale tenants. Out of scope now.

### 8.3 Integration events vs denormalization — trade-offs

| Approach | Pros | Cons |
|---|---|---|
| **Denormalize** (snapshot column, kept in sync by integration events) | Zero-join reads. Fast. Predictable p95. | Stale until the event is processed; requires cascade-update handlers for every mutable field. |
| **Look up on demand** (query Contacts on read) | Always fresh. No cascade handlers. | N network calls per page; couples read latency to the other module's uptime. |
| **Read model** (materialized projection) | Denormalized + fully controlled + optimizable. | Extra storage, extra pipeline, extra ops. Only worth it at scale. |

**Rule of thumb we adopt:** denormalize when the field is (a) read-hot, (b) rarely mutated, (c) tolerable to be a few seconds stale. Otherwise, join at the API layer (BFF composition) or wait for a read model.

### 8.4 Consistency guarantees

- Within `jobs.*`: **strong** (single-tx). EF `SaveChangesAsync` + `InsertOutboxMessagesInterceptor` ensures domain rows and their outbox rows commit together.
- Across schemas (`jobs → billing → notifications`): **eventual, at-least-once**, bounded by the outbox poll interval (10 s). Consumers idempotent via `processed_inbox`.
- Ordering: **per-aggregate**, not global. Fine for this domain because a `JobCompleted` event has no relevant "later" event that must arrive first at the same aggregate.

---

## 9. Migration plan

### 9.1 Per-module DbContext, per-module migrations

Each module owns its `DbContext` and its migrations under `Modules/<Module>/<Module>.Infrastructure/Persistence/Migrations/`. The API host runs all migrators sequentially at startup in Development; in Production a CLI tool (`dotnet ef database update`) is invoked from CI/CD.

### 9.2 Initial migration content

`0001_Init` for Jobs:
1. `CREATE SCHEMA jobs;`
2. `CREATE EXTENSION IF NOT EXISTS "pgcrypto"; CREATE EXTENSION IF NOT EXISTS "btree_gin";`
3. `CREATE TABLE jobs.jobs (...);` (including the generated `search_vector` column)
4. `CREATE TABLE jobs.job_photos (...);`
5. `CREATE TABLE jobs.outbox_messages (...);`
6. All indexes from §4.
7. Seed baseline data if needed (for MVP: none — tenants are provisioned via Identity).

For the FTS + check constraints EF doesn't model natively, we drop into `migrationBuilder.Sql(...)` inside the generated migration.

### 9.3 Verifying the migration

Locally:
```powershell
dotnet ef migrations add InitialCreate `
    --project src/Modules/Jobs/Jobs.Infrastructure `
    --startup-project src/Api `
    --context JobsDbContext `
    --output-dir Persistence/Migrations

dotnet ef database update `
    --project src/Modules/Jobs/Jobs.Infrastructure `
    --startup-project src/Api `
    --context JobsDbContext
```

Integration tests run migrations against a Testcontainers Postgres to catch drift between DDL and Fluent config.

---

## 10. Backup, ops, and safety

| Concern | Approach |
|---|---|
| Backups | Managed Postgres with PITR (15-min RPO). |
| Migrations in prod | Applied out-of-band before app rollout; app runs `--no-migrate` in prod. |
| Schema drift | Nightly `pg_dump --schema-only` compared to a committed baseline in CI. |
| Poison messages | Outbox rows with `attempts >= N` (configurable) are flagged, moved to `outbox_messages_dead` via a Hangfire recurring job. |
| Row-level security | Not enabled for MVP. Enforcement lives in application (`ITenantContext` + EF filter). RLS is a future defense-in-depth. |
| Connection pool | Npgsql pool sized to `2 × vCPU + 1`. PgBouncer optional at scale. |

---

## 11. Related documents

- 01 — Domain model (aggregates, VOs, events).
- 03 — Backend solution structure (where DbContexts live).
- 06 — Async messaging (outbox interceptor + Hangfire processor).
- ADR-0004 — Multi-tenancy: shared DB, discriminator column.
- ADR-0005 — Schema per module.

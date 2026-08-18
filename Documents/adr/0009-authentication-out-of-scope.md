# ADR-0009 — Authentication (login flow) out of scope for this iteration

- **Status:** Accepted
- **Date:** 2026-08-17
- **Deciders:** Team Lead

## Context

The assessment (*Technical Assessment — Senior Fullstack Engineer, Next.js + .NET*) has a hard 12-hour budget. Authentication appears in the requirements exactly once, inside the **Section 6.1 — Architecture Diagram** description:

> *"5. **Cross-cutting:** Authentication, multi-tenancy, error handling"*

There is no functional requirement anywhere in the assessment for:

- A sign-in / sign-up UI or `/auth/*` HTTP endpoints.
- A `users` table or password storage.
- A JWT issuance endpoint.
- Roles / permissions / RBAC.
- Session management or refresh tokens.

The **rubric** (100 pts across 6 sections) does not grade auth as a functional feature. It only grades whether authentication is **shown** in the architecture diagram as a cross-cutting concern (Section 6.1, 3 pts total).

**Multi-tenancy IS a functional requirement** — the `Job` aggregate must carry `OrganizationId`, every table must be tenant-scoped, and cross-tenant leakage must be prevented. That we do (see [ADR-0004](./0004-multi-tenancy-shared-db-discriminator.md)).

## Decision

**Do not implement a login flow in this iteration.** Instead:

1. Ship the tenancy layer end-to-end, using the `X-Organization-Id: <GUID>` HTTP header as the tenant carrier for every request. Requests without a valid tenant fail with `MissingTenantException` → `400 problem+json`.
2. Keep the JWT infrastructure **wired but disabled by default**. `Program.cs` reads a `Jwt:Enabled` flag; when true, it registers `AddJwtBearer(...)` and `UseAuthentication()`. `JwtTenantContext` already inspects the `org_id` claim first, falling back to the header — so flipping the flag is a zero-code change on the tenant path.
3. Document authentication as a **cross-cutting concern** in the architecture diagram and in `00-architecture-overview.md §7`, explicitly labeled *"JWT prepared / X-Organization-Id today"*.

## Alternatives considered

| Option | Cost | Assessment payoff | Chosen? |
|---|---|---|---|
| **A — Document only (this ADR)** | 15 min | Covers Section 6.1 diagram requirement. No production risk. | ✅ |
| B — Mock login page: user picks/enters a workspace GUID stored in cookie | 1–2 h | Slightly more visual. Still not "real" auth. | ✗ |
| C — Full JWT flow: `users` table + password hashing + `POST /auth/login` + client interceptor | 3–5 h | Above rubric expectations. Displaces time from other rubric sections (Testing, Docker Compose, ADRs). | ✗ |

Option A is chosen because:
- The rubric does not reward the extra 3–5 h that Option C would consume.
- Options B and C both risk **negative marks** in Testing / DDD / DevOps sections if they cause other deliverables to slip.
- The current design cleanly supports flipping to real JWT later — no refactor required.

## Consequences

**Positive**
- Full 12-hour budget goes to rubric-graded sections: DDD, CQRS, Outbox, Testing, FSD, TypeScript utility types.
- Multi-tenant isolation is testable end-to-end today (unit + integration tests assert cross-tenant queries return zero rows).
- The upgrade path to real JWT is a config flip, not a redesign — the `ITenantContext` abstraction hides the source of the tenant ID (header today, claim tomorrow).

**Negative**
- The demo application accepts any GUID as tenant. This is only acceptable for local development — production would enable `Jwt:Enabled=true` and require signed tokens.
- Anyone with the API URL can call it. In production this would be closed by JWT and, optionally, network-level protections.
- If the evaluator personally weights auth more heavily than the rubric implies, we may lose informal credit here. Mitigation: the ADR and diagram make the deliberate scoping visible.

## Follow-up work when authentication is prioritized

Concrete plan for the next iteration (~4 h):

1. **`Identity` module** (new bounded context, own schema):
   - `identity.users(id, organization_id, email, password_hash, created_at)`.
   - `identity.organizations(id, name, created_at)` — real tenant registry to validate the header against.
2. **Endpoints:**
   - `POST /api/v1/auth/register` (or admin-only invite) — hash with Argon2id.
   - `POST /api/v1/auth/login` — verify + issue JWT (`sub`, `org_id`, `exp`, `iat`, `iss`, `aud`).
   - `POST /api/v1/auth/refresh` — sliding refresh tokens with rotation.
3. **`Jwt:Enabled=true`** in `appsettings.Production.json`. `Authority` + `Audience` populated.
4. **Frontend:**
   - `/login` route with credentials form.
   - Client fetch interceptor pins `Authorization: Bearer <token>` and refreshes on 401.
   - Remove `DEFAULT_ORG_ID` server env var — tenant comes exclusively from the claim.
5. **Row-Level Security in Postgres** as defense-in-depth (`SET LOCAL app.tenant_id = ...` per connection + policies on every table).

## References

- `00-architecture-overview.md` §7 (cross-cutting concerns) and §15 (out of scope).
- `ADR-0004` — multi-tenancy strategy.
- `technical-assessment-fullstack-senior 3.md` — Section 6.1 (Architecture Diagram — cross-cutting concerns) and Sections 1–5 rubric (no auth functional criteria).
- `src/Host/JobTracker.Api/Infrastructure/Tenant/JwtTenantContext.cs` — dual-source tenant resolution.
- `src/Host/JobTracker.Api/Program.cs` — `Jwt:Enabled` flag and conditional `AddJwtBearer` registration.

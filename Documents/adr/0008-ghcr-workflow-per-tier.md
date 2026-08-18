# ADR-0008 — CI: GHCR + Workflow per Tier

- **Status:** Accepted
- **Date:** 2026-08-14
- **Deciders:** Team Lead

## Context

We need CI that:
- Runs fast (path-filtered per tier).
- Fails PRs on lint / test / arch violations / coverage.
- Publishes container images somewhere accessible for staging / prod pulls.
- Doesn't bring in a new registry service.

Options for image hosting:

| Option | Pros | Cons |
|---|---|---|
| Docker Hub | Public/free tier known. | Rate limits on anonymous pulls; separate credential set. |
| **GHCR (GitHub Container Registry)** | Same auth as GitHub Actions (`GITHUB_TOKEN`); free for public repos; org-level access controls. | Tightly coupled to GitHub. |
| ACR / ECR | Native cloud integration. | Extra provisioning; multi-cloud not needed yet. |

Workflow layout options:

| Option | Pros | Cons |
|---|---|---|
| One monolithic workflow | Simple. | Every push runs everything → slow. |
| **Workflow per tier (backend, frontend, e2e)** with `paths:` filters | Fast; independent. | Slight duplication. |

## Decision

**Adopt GHCR + one workflow per tier**, plus a shared `e2e` workflow that boots the full stack.

- `.github/workflows/backend.yml` — build, test (unit + arch + integration via Testcontainers), export OpenAPI, publish `ghcr.io/<repo>/api:<sha>` on `main`.
- `.github/workflows/frontend.yml` — lint, typecheck, vitest, codegen sanity against the OpenAPI artifact, publish `ghcr.io/<repo>/web:<sha>` on `main`.
- `.github/workflows/e2e.yml` — `docker compose up`, wait for readiness, run Playwright, upload traces/videos on failure.

Images are tagged with `:<git-sha>` (immutable) and `:latest` (moving). Cache is `type=gha` for buildx layer caching.

## Consequences

**Positive**
- PRs get feedback in ~5 min for BE, ~2 min for FE.
- Zero external accounts to manage.
- OpenAPI contract flows from BE workflow → FE workflow (artifact download).
- Image SHAs are the deployment unit — reproducible.

**Negative**
- Tied to GitHub. If we ever move providers we swap workflows + registry.
- Concurrency on `main` needs a `concurrency:` group to avoid duplicate image publishes (added).

## References

- 08-devops-cicd.md §6
- ADR-0002 (Hangfire — no extra queue infra either)

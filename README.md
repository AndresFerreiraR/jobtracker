# JobTracker

Multi-tenant roofing job management system built for the *Senior Fullstack Engineer* technical assessment.

- **Backend** — ASP.NET Core 9 (Modular Monolith, CQRS + MediatR, EF Core + Postgres, Outbox, Result pattern).
- **Frontend** — Next.js 15 (App Router, React 19, Feature-Sliced Design, TypeScript strict, Tailwind).
- **Database** — PostgreSQL 16.

Full stack ships as a single `docker compose` command — no local .NET / Node / Postgres installation required.

---

## Prerequisites

Only **Docker Desktop** (or Docker Engine + Compose v2).

- macOS: <https://docs.docker.com/desktop/install/mac-install/>
- Windows: <https://docs.docker.com/desktop/install/windows-install/>
- Linux: <https://docs.docker.com/engine/install/>

Verify:

```bash
docker --version           # >= 24.x
docker compose version     # >= 2.20 (v2 syntax)
```

Docker Desktop should be running before executing the commands below.

---

## Run the full stack

```bash
git clone <this-repo-url> jobtracker
cd jobtracker
docker compose up --build
```

That's it. Three containers come up:

| Service    | Image                              | Host URL                        | Container port |
|------------|------------------------------------|---------------------------------|----------------|
| `postgres` | `postgres:16-alpine`               | `localhost:5432`                | 5432           |
| `api`      | built from `./api`                 | <http://localhost:59081>        | 8080           |
| `web`      | built from `./web`                 | <http://localhost:59082>        | 3000           |

**Open the app at <http://localhost:59082>.**

### What happens on first boot

1. Docker pulls `postgres:16-alpine` (~80 MB) if not cached.
2. Docker **builds** the `api` and `web` images from source. Expect **3–5 min** the first time (`dotnet restore` + `npm ci` + `next build`). Subsequent runs use the layer cache and take seconds.
3. `postgres` starts. `api` and `web` wait for it via health checks.
4. `api` starts, connects to Postgres, and **applies all EF Core migrations automatically** (`Database:AutoMigrate=true` in compose). Watch the log line:

    ```text
    info: JobTracker.Api.Program[0] Applying database migrations...
    info: JobTracker.Api.Program[0] Database migrations applied.
    ```

5. `web` starts and serves the Next.js production build.

You'll see logs from all three services interleaved. `Ctrl+C` stops them; use `-d` to run detached (see below).

### Useful URLs

| URL                                          | What it is                                        |
|----------------------------------------------|---------------------------------------------------|
| <http://localhost:59082>                     | Web app (jobs list, create/schedule/complete)     |
| <http://localhost:59082/jobs>                | Jobs board                                        |
| <http://localhost:59081/health>              | API health check                                  |
| <http://localhost:59081/swagger>             | Swagger UI *(only when the container runs `ASPNETCORE_ENVIRONMENT=Development`)* |
| `postgres://jobtracker:jobtracker@localhost:5432/jobtracker` | Postgres, if you want to inspect data |

---

## Common commands

```bash
docker compose up --build         # build + start (foreground)
docker compose up -d --build      # build + start (detached, no log stream)
docker compose logs -f api        # follow logs of a single service
docker compose ps                 # list running containers + health status
docker compose stop               # stop containers, keep data
docker compose start              # bring stopped containers back
docker compose down               # stop + remove containers (volumes kept)
docker compose down -v            # stop + remove containers + wipe volumes
docker compose exec api sh        # shell inside the API container
docker compose exec postgres psql -U jobtracker jobtracker
```

### Reset from scratch

```bash
docker compose down -v            # drops postgres-data + api-uploads volumes
docker compose up --build         # migrations run again on fresh schema
```

Use this after schema-breaking changes or when you want a pristine demo.

---

## Ports and environment

Ports chosen to avoid collisions with typical local dev tools:

- **`59081`** for the API (avoids `5000/5001` used by other .NET projects).
- **`59082`** for the Web (avoids `3000/8080` used by other Node projects).
- **`5432`** for Postgres — the container binds to the standard port on the host. **If you already have a Postgres running locally on 5432**, either stop it (`brew services stop postgresql` on macOS) or change the mapping in `docker-compose.yml` (see *Troubleshooting*).

The `api` container talks to Postgres via the compose network (`postgres:5432`, not `localhost`). The `web` container's server-side fetches use `http://api:8080` (compose DNS); the browser bundle uses `http://localhost:59081` (host port).

---

## What the compose file gives you

`docker-compose.yml` in the repo root:

- **`postgres`** — PostgreSQL 16 with a named volume (`postgres-data`) so schema and rows survive container restarts.
- **`api`** — the .NET Web API with:
  - Health check on `/health`.
  - **Automatic EF migrations on startup** (`Database__AutoMigrate=true`).
  - Named volume (`api-uploads`) mounted at `/app/uploads` for photos + signatures. Persists across restarts.
  - JWT authentication wired but disabled (`Jwt__Enabled=false`). Tenant travels in the `X-Organization-Id` header — see the architecture docs and [ADR-0009](./Documents/adr/0009-authentication-out-of-scope.md).
- **`web`** — Next.js production build with:
  - Server-side fetches → `http://api:8080` (in-network DNS).
  - Browser fetches → `http://localhost:59081` (baked into the JS bundle at build time via `NEXT_PUBLIC_API_BASE_URL` build arg).
  - `DEFAULT_ORG_ID` seeded so the demo works without a login screen.

Both containers run as non-root (user `app` / `nextjs`). The API base image is Alpine (~120 MB); the Web runtime image uses `next/standalone` (~180 MB).

---

## Troubleshooting

### Port already allocated (`Bind for 0.0.0.0:5432 failed`)

Another Postgres is holding the host port. Either:

**Option A** — stop the offending process:

```bash
# macOS Homebrew
brew services stop postgresql
# or another docker container
docker stop <container-name>
```

**Option B** — remap in `docker-compose.yml`:

```yaml
postgres:
  ports:
    - "5433:5432"     # host 5433, container still 5432
```

Same trick works for `59081` and `59082` if those clash.

### `Permission denied` writing to `/app/uploads`

Happens if the `api-uploads` volume was created before the current Dockerfile chown fix. Reset the volume:

```bash
docker compose down -v
docker compose up --build
```

### Migrations didn't run

Check `docker compose logs api` for `Applying database migrations...`. If missing:
- Confirm `Database__AutoMigrate=true` is in the `api` service env vars.
- Confirm the Postgres container is healthy (`docker compose ps`).
- Reset with `docker compose down -v && docker compose up --build`.

### Frontend hits `http://localhost:5000` or another wrong URL

The browser URL is baked at build time. Rebuild with the correct arg:

```bash
docker compose build --no-cache web
docker compose up
```

Or override the arg (`--build-arg NEXT_PUBLIC_API_BASE_URL=http://localhost:59081` in the Dockerfile via compose `args`).

---

## Project layout

```
.
├── api/                                .NET 9 solution
│   ├── src/
│   │   ├── BuildingBlocks/             SharedKernel, Application, Infrastructure, Presentation
│   │   ├── Modules/Jobs/               Domain, Application, Infrastructure, Presentation, IntegrationEvents
│   │   └── Host/JobTracker.Api/        Composition root, Program.cs
│   ├── tests/                          Unit, Architecture, Integration
│   ├── JobTracker.sln
│   └── Dockerfile
├── web/                                Next.js 15 App Router
│   ├── app/                            Route segments (RSC + client components)
│   ├── entities/                       Domain-shaped types + fetchers (FSD)
│   ├── features/                       Use-case slices
│   ├── widgets/                        Composed UI blocks
│   ├── shared/                         Reusable primitives, config, http
│   ├── playwright/                     E2E tests
│   └── Dockerfile
├── Documents/                          Architecture, ADRs, principles & patterns
│   ├── 00-architecture-overview.md
│   ├── ...
│   └── adr/0001..0009-*.md
├── Infra/                              OTel collector config + GitHub Actions workflow templates
├── docker-compose.yml                  ← used by the commands above
└── README.md
```

---

## Design & architecture

See `Documents/`:

- [00 — Architecture overview](./Documents/00-architecture-overview.md) — start here.
- [01 — Domain model](./Documents/01-domain-model.md)
- [02 — Database design](./Documents/02-database-design.md)
- [03 — Backend solution](./Documents/03-backend-solution.md)
- [04 — API contracts](./Documents/04-api-contracts.md)
- [05 — Frontend architecture](./Documents/05-frontend-architecture.md)
- [06 — Async messaging (Outbox)](./Documents/06-async-messaging.md)
- [07 — Testing strategy](./Documents/07-testing-strategy.md)
- [08 — DevOps & CI/CD](./Documents/08-devops-cicd.md)
- [09 — Principles & patterns](./Documents/09-principles-and-patterns.md)

Architectural decisions in `Documents/adr/`:

- [ADR-0001 — Modular monolith over microservices](./Documents/adr/0001-modular-monolith-over-microservices.md)
- [ADR-0002 — Outbox with Hangfire, no external broker](./Documents/adr/0002-outbox-hangfire-no-external-broker.md)
- [ADR-0003 — Result pattern over exceptions](./Documents/adr/0003-result-pattern-over-exceptions.md)
- [ADR-0004 — Multi-tenancy: shared DB, discriminator column](./Documents/adr/0004-multi-tenancy-shared-db-discriminator.md)
- [ADR-0005 — Schema per module (Postgres)](./Documents/adr/0005-schema-per-module.md)
- [ADR-0006 — BigSerial PK on outbox](./Documents/adr/0006-bigserial-pk-on-outbox.md)
- [ADR-0007 — Domain-to-integration event via handler](./Documents/adr/0007-domain-to-integration-event-via-handler.md)
- [ADR-0008 — GHCR workflow per tier](./Documents/adr/0008-ghcr-workflow-per-tier.md)
- [ADR-0009 — Authentication (login flow) out of scope](./Documents/adr/0009-authentication-out-of-scope.md)

---

## Assumptions & scope notes

- **No login flow.** Tenant identity travels in the `X-Organization-Id` header. JWT infrastructure is wired and can be turned on with a config flip — see [ADR-0009](./Documents/adr/0009-authentication-out-of-scope.md).
- **CORS is open** (`AllowAnyOrigin/Header/Method`) — appropriate for demo/dev. Production would restrict origins via `WithOrigins(...)`.
- **Migrations run on API startup**, not during `docker build`. Same image works against any Postgres; the build stays reproducible.
- **1 API replica.** For horizontal scaling, migrations should move to a dedicated init container (see the *Future work* section in [ADR-0009](./Documents/adr/0009-authentication-out-of-scope.md)).
- **Local file storage** for photos + signatures. In production this would be swapped for S3 / Azure Blob behind the existing `IFileStorage` abstraction.

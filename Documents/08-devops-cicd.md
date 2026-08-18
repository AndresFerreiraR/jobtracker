# 08 — DevOps & CI/CD

> Goal: reproducible local dev via `docker compose up`, deterministic CI on GitHub Actions, minimal-surface OpenTelemetry from day one.
> Non-goal in this iteration: production deployment topology. That lives in a follow-up doc when we pick a target platform.

---

## 1. Repository layout for DevOps

```
Repositories/
├── api/
│   ├── Dockerfile                     # api multi-stage
│   ├── src/…
│   └── tests/…
├── web/
│   ├── Dockerfile                     # nextjs standalone multi-stage
│   └── src/…
├── infra/
│   ├── docker-compose.yml             # local stack (postgres + api + web + otel-collector)
│   ├── docker-compose.override.yml    # dev-only overrides (volumes, hot reload)
│   ├── otel-collector.yaml            # OTLP receiver → console/OTLP exporter
│   └── postgres/
│       └── init.sql                   # CREATE DATABASE + CREATE USER (bootstrap)
├── .github/
│   └── workflows/
│       ├── backend.yml
│       ├── frontend.yml
│       └── e2e.yml
└── Documents/
    └── …
```

Docker + compose live under `infra/` so both `api/` and `web/` stay clean.

---

## 2. Backend Dockerfile (multi-stage)

`api/Dockerfile`:

```dockerfile
# syntax=docker/dockerfile:1.7
############################################
# 1) Restore + build
############################################
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only what's needed to restore first (better layer caching).
COPY *.sln Directory.Build.props Directory.Packages.props nuget.config ./
COPY src/ ./src/

RUN dotnet restore JobTracker.sln
RUN dotnet publish src/Api/JobTracker.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

############################################
# 2) Runtime
############################################
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Non-root
RUN useradd --uid 10001 --create-home --home-dir /home/app app
USER app

# Culture / TLS defaults
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317 \
    OTEL_SERVICE_NAME=JobTracker.Api \
    OTEL_TRACES_SAMPLER=parentbased_traceidratio \
    OTEL_TRACES_SAMPLER_ARG=1.0

COPY --from=build --chown=app:app /app/publish ./

EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --retries=5 --start-period=15s \
    CMD wget -qO- http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "JobTracker.Api.dll"]
```

**Notes:**
- Two-stage: SDK for build, ASP.NET runtime image is much slimmer.
- Non-root user (10001) — good hygiene, satisfies most k8s pod security policies.
- Layer-cache-friendly restore: manifests first, sources next.
- OTLP env vars picked up by the OTel SDK.

Health endpoints (added in `Api/Program.cs`):

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(cfg.GetConnectionString("JobTracker")!, name: "postgres")
    .AddCheck<HangfireHealthCheck>("hangfire");

app.MapHealthChecks("/health/live",  new HealthCheckOptions { Predicate = _ => false });      // liveness: process alive
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = _ => true  });      // readiness: deps OK
```

---

## 3. Frontend Dockerfile (Next.js 15 standalone)

`web/Dockerfile`:

```dockerfile
# syntax=docker/dockerfile:1.7
############################################
# 1) Dependencies
############################################
FROM node:20-alpine AS deps
WORKDIR /app
COPY package.json pnpm-lock.yaml ./
RUN corepack enable && corepack prepare pnpm@latest --activate
RUN pnpm install --frozen-lockfile

############################################
# 2) Build
############################################
FROM node:20-alpine AS build
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1
COPY --from=deps /app/node_modules ./node_modules
COPY . .

# Next 15 standalone output must be enabled in next.config.ts:
#   export default { output: "standalone", ... }
RUN corepack enable && corepack prepare pnpm@latest --activate && pnpm build

############################################
# 3) Runtime
############################################
FROM node:20-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000

RUN addgroup -g 10001 app && adduser -D -u 10001 -G app app
USER app

# Standalone output: server.js + minimal node_modules
COPY --from=build --chown=app:app /app/.next/standalone ./
COPY --from=build --chown=app:app /app/.next/static ./.next/static
COPY --from=build --chown=app:app /app/public ./public

EXPOSE 3000
HEALTHCHECK --interval=15s --timeout=3s --retries=5 CMD wget -qO- http://localhost:3000/api/health || exit 1

ENTRYPOINT ["node", "server.js"]
```

`next.config.ts`:
```ts
export default { output: "standalone", reactStrictMode: true, poweredByHeader: false };
```

An `app/api/health/route.ts` simply returns `200` for the container healthcheck.

---

## 4. docker-compose

`infra/docker-compose.yml`:

```yaml
name: jobtracker

services:
  postgres:
    image: postgres:16-alpine
    container_name: jt-postgres
    environment:
      POSTGRES_DB: jobtracker
      POSTGRES_USER: app
      POSTGRES_PASSWORD: app
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./postgres/init.sql:/docker-entrypoint-initdb.d/00-init.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U app -d jobtracker"]
      interval: 5s
      timeout: 3s
      retries: 20

  otel-collector:
    image: otel/opentelemetry-collector-contrib:0.106.1
    container_name: jt-otel
    command: ["--config=/etc/otelcol/config.yaml"]
    volumes:
      - ./otel-collector.yaml:/etc/otelcol/config.yaml:ro
    ports:
      - "4317:4317"   # OTLP gRPC
      - "4318:4318"   # OTLP HTTP
    depends_on: [ postgres ]

  api:
    build:
      context: ../api
      dockerfile: Dockerfile
    container_name: jt-api
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__JobTracker: Host=postgres;Database=jobtracker;Username=app;Password=app
      OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317
      OTEL_SERVICE_NAME: JobTracker.Api
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
      otel-collector:
        condition: service_started
    healthcheck:
      test: ["CMD", "wget", "-qO-", "http://localhost:8080/health/ready"]
      interval: 10s
      timeout: 3s
      retries: 30

  web:
    build:
      context: ../web
      dockerfile: Dockerfile
    container_name: jt-web
    environment:
      NEXT_PUBLIC_APP_ENV: local
      API_BASE_URL: http://api:8080
    ports:
      - "3000:3000"
    depends_on:
      api:
        condition: service_healthy

volumes:
  pgdata:
```

`infra/docker-compose.override.yml` (loaded automatically by `docker compose up`, dev-friendly):

```yaml
services:
  api:
    build:
      target: build     # keep the SDK stage so we can run `dotnet watch`
    command: ["dotnet", "watch", "--project", "src/Api/JobTracker.Api.csproj", "--no-hot-reload"]
    volumes:
      - ../api:/src

  web:
    command: ["pnpm", "dev"]
    volumes:
      - ../web:/app
      - /app/node_modules
```

`infra/otel-collector.yaml`:

```yaml
receivers:
  otlp:
    protocols:
      grpc: { endpoint: 0.0.0.0:4317 }
      http: { endpoint: 0.0.0.0:4318 }

processors:
  batch: {}

exporters:
  debug:
    verbosity: normal
  # Uncomment when a real backend is wired:
  # otlp/jaeger:
  #   endpoint: jaeger:4317
  #   tls: { insecure: true }

service:
  pipelines:
    traces:
      receivers:  [ otlp ]
      processors: [ batch ]
      exporters:  [ debug ]
    metrics:
      receivers:  [ otlp ]
      processors: [ batch ]
      exporters:  [ debug ]
```

`infra/postgres/init.sql`:

```sql
-- One-time bootstrap (executed by the postgres container on first boot).
-- The application connects as 'app', which has full rights to the 'jobtracker' database.
-- EF migrations run at startup in Development.
```

Everyday usage:

```powershell
cd infra
docker compose up -d postgres otel-collector
# then run api + web on host for max iteration speed:
dotnet run --project ..\api\src\Host\JobTracker.Api
npm --prefix ..\web run dev

# or run everything in-cluster:
docker compose up --build
```

---

## 5. OpenTelemetry wiring (backend)

`Api/Program.cs`:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService("JobTracker.Api")
        .AddAttributes(new KeyValuePair<string, object>[] {
            new("deployment.environment", builder.Environment.EnvironmentName)
        }))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation(o => o.RecordException = true)
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = true)
        .AddSource("JobTracker")                 // custom ActivitySource used inside outbox processor
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("JobTracker.Outbox")           // custom Meter (from 06-async-messaging.md §9)
        .AddOtlpExporter());
```

Environment variables control the endpoint (`OTEL_EXPORTER_OTLP_ENDPOINT`). This is picked up automatically.

Custom span in the outbox processor:

```csharp
private static readonly ActivitySource Activity = new("JobTracker");

public async Task ProcessAsync(...)
{
    using var span = Activity.StartActivity("outbox.batch");
    // per-event child spans as well.
}
```

Frontend OTel: for MVP we rely on Next.js `traceparent` propagation (fetch propagates by default when `next.config.ts` opts in). A dedicated `@vercel/otel` package can be added when the app becomes distributed.

---

## 6. GitHub Actions workflows

### 6.1 Backend workflow — `.github/workflows/backend.yml`

```yaml
name: backend

on:
  push:
    paths: [ 'api/**', '.github/workflows/backend.yml' ]
  pull_request:
    paths: [ 'api/**' ]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16-alpine
        ports: [ 5432:5432 ]
        env:
          POSTGRES_USER: app
          POSTGRES_PASSWORD: app
          POSTGRES_DB: jobtracker_ci
        options: >-
          --health-cmd pg_isready --health-interval 5s --health-timeout 3s --health-retries 20
    env:
      DOTNET_NOLOGO: true
      DOTNET_CLI_TELEMETRY_OPTOUT: 1
      ConnectionStrings__JobTracker: Host=localhost;Database=jobtracker_ci;Username=app;Password=app
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore
        working-directory: api
        run: dotnet restore

      - name: Build
        working-directory: api
        run: dotnet build --no-restore -c Release

      - name: Test (unit + architecture)
        working-directory: api
        run: >
          dotnet test --no-build -c Release
          --filter "FullyQualifiedName!~IntegrationTests"
          --logger "trx;LogFileName=unit.trx"
          --collect:"XPlat Code Coverage"

      - name: Test (integration)
        working-directory: api
        # These use Testcontainers.PostgreSql, which spawns its own container
        # via the docker socket exposed by GitHub-hosted runners.
        run: >
          dotnet test --no-build -c Release
          --filter "FullyQualifiedName~IntegrationTests"
          --logger "trx;LogFileName=integration.trx"

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: backend-tests
          path: api/**/TestResults/*.trx

      - name: Export OpenAPI (for FE codegen)
        working-directory: api
        run: |
          dotnet run --project src/Api --no-build --launch-profile export-openapi > swagger.json
      - uses: actions/upload-artifact@v4
        with: { name: openapi, path: api/swagger.json }

  build-image:
    needs: test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    permissions: { contents: read, packages: write }
    steps:
      - uses: actions/checkout@v4
      - uses: docker/setup-buildx-action@v3
      - uses: docker/login-action@v3
        with: { registry: ghcr.io, username: ${{ github.actor }}, password: ${{ secrets.GITHUB_TOKEN }} }
      - uses: docker/build-push-action@v6
        with:
          context: api
          push: true
          tags: |
            ghcr.io/${{ github.repository }}/api:latest
            ghcr.io/${{ github.repository }}/api:${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

### 6.2 Frontend workflow — `.github/workflows/frontend.yml`

```yaml
name: frontend

on:
  push:
    paths: [ 'web/**', '.github/workflows/frontend.yml' ]
  pull_request:
    paths: [ 'web/**' ]

jobs:
  test:
    runs-on: ubuntu-latest
    defaults:
      run: { working-directory: web }
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20', cache: 'pnpm' }
      - uses: pnpm/action-setup@v4
        with: { version: 9 }
      - run: pnpm install --frozen-lockfile
      - run: pnpm lint
      - run: pnpm typecheck
      - run: pnpm test --coverage
      - name: Fetch OpenAPI artifact
        uses: actions/download-artifact@v4
        with: { name: openapi, path: web/api-schema/ }
      - name: Codegen sanity (openapi-typescript)
        run: pnpm openapi:generate

  build-image:
    needs: test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    permissions: { contents: read, packages: write }
    steps:
      - uses: actions/checkout@v4
      - uses: docker/setup-buildx-action@v3
      - uses: docker/login-action@v3
        with: { registry: ghcr.io, username: ${{ github.actor }}, password: ${{ secrets.GITHUB_TOKEN }} }
      - uses: docker/build-push-action@v6
        with:
          context: web
          push: true
          tags: |
            ghcr.io/${{ github.repository }}/web:latest
            ghcr.io/${{ github.repository }}/web:${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

### 6.3 E2E workflow — `.github/workflows/e2e.yml`

Runs against a fully-composed stack so the tests hit real HTTP:

```yaml
name: e2e

on:
  pull_request:
    paths: [ 'api/**', 'web/**', 'infra/**' ]

jobs:
  playwright:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: docker/setup-buildx-action@v3
      - name: Boot the stack
        working-directory: infra
        run: docker compose up -d --build
      - name: Wait for readiness
        run: |
          for i in {1..60}; do
            curl -fsS http://localhost:8080/health/ready && \
            curl -fsS http://localhost:3000/api/health && break || sleep 2
          done
      - uses: actions/setup-node@v4
        with: { node-version: '20', cache: 'pnpm' }
      - uses: pnpm/action-setup@v4
        with: { version: 9 }
      - name: Install FE deps
        working-directory: web
        run: pnpm install --frozen-lockfile
      - name: Playwright browsers
        working-directory: web
        run: pnpm exec playwright install --with-deps
      - name: Run E2E
        working-directory: web
        env:
          E2E_BASE_URL: http://localhost:3000
        run: pnpm exec playwright test
      - name: Upload traces / videos on failure
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report
          path: web/playwright-report
```

---

## 7. Secrets & configuration

- **Local dev**: everything in `docker-compose.yml` env blocks. Plaintext is fine for `app/app` postgres.
- **CI**: only `GITHUB_TOKEN` is used (for GHCR). No prod secrets touched in this iteration.
- **Production (future)**: secrets injected via the platform's secret store (Container Apps secrets, Kubernetes External Secrets, etc.). App reads via `IConfiguration` (Azure Key Vault provider or env-var passthrough).
- **Never commit**: `.env`, private keys, real JWT signing keys. `.gitignore` already excludes `.env*` (except `.env.example`).

---

## 8. What each pipeline enforces

| Check | Where | Fails PR? |
|---|---|---|
| dotnet build | backend.test | ✓ |
| Unit tests | backend.test | ✓ |
| Architecture tests | backend.test | ✓ |
| Integration tests (Testcontainers) | backend.test | ✓ |
| Coverage threshold | backend.test | ✓ (soft-fail warning to start; hard-fail once baseline set) |
| OpenAPI export | backend.test | ✓ |
| eslint | frontend.test | ✓ |
| tsc --noEmit | frontend.test | ✓ |
| Vitest | frontend.test | ✓ |
| Codegen sanity (schema matches types) | frontend.test | ✓ |
| Playwright E2E | e2e | ✓ |
| Docker images build | *.build-image | ✓ (main only) |

---

## 9. Contract sync between BE and FE

1. Backend CI exports `swagger.json` as a workflow artifact.
2. Frontend CI downloads it and runs `openapi-typescript` → produces `src/infrastructure/api/generated/schema.d.ts`.
3. `pnpm typecheck` fails if the schema changed in a breaking way.
4. Developers regenerate locally with `pnpm openapi:generate` before pushing.

Long-term, the schema can be published as an npm package on GHCR for stable versioning.

---

## 10. Making the local loop fast

- **`docker compose up -d postgres otel-collector`** during active dev; run api + web natively for hot reload.
- **`dotnet watch --project src/Api`** picks up C# changes without container rebuild.
- **`pnpm dev`** with Next.js 15 turbopack.
- Testcontainers reuses containers across runs (`.withReuse(true)`) in local mode via `~/.testcontainers.properties`.

---

## 11. Related documents

- 03 — Backend solution structure (Program.cs wiring OTel).
- 05 — Frontend architecture (env config + api client).
- 06 — Async messaging (Hangfire dashboard exposed at `/hangfire`).
- 07 — Testing strategy (what runs in CI).
- ADR-0008 — GHCR + workflow-per-tier layout.

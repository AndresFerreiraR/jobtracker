# Infra

Infrastructure-adjacent assets that live outside the app code:

```
Infra/
├── otel/config.yaml         # OpenTelemetry collector pipelines (reserved for observability wiring)
└── github-workflows/        # GitHub Actions workflows — move to .github/workflows/ before push
    ├── backend.yml
    ├── frontend.yml
    └── e2e.yml
```

## Docker Compose

**Not here.** The single source of truth is `docker-compose.yml` at the repository root. See the top-level `README.md` for run instructions.

## OpenTelemetry

`otel/config.yaml` is a ready-to-use collector configuration for the OTLP exporter. It is **not wired into the compose file** today — the backend has OTel SDK packages installed and emits traces/metrics to whatever `OTEL_EXPORTER_OTLP_ENDPOINT` points to, so bringing up a collector is a matter of adding one more service to the root `docker-compose.yml` (or a `profiles: [observability]` block) that mounts this config.

## GitHub Actions

The three workflows under `github-workflows/` are placed here for review. In a real repository they must live under `.github/workflows/` at the root of the git repo so GitHub picks them up automatically.

```bash
# from the repo root
mkdir -p .github/workflows
cp Infra/github-workflows/*.yml .github/workflows/
```

- `backend.yml` — restore/build/test the .NET solution, push image to GHCR on `main`.
- `frontend.yml` — typecheck/lint/test/build the Next.js app, push image to GHCR on `main`.
- `e2e.yml` — nightly + on-push Playwright run against a fresh Postgres + published API + Next.js app.

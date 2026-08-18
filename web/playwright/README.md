# Playwright E2E

## Layout

```
playwright/
├── pom/                 # Page Object Models
│   ├── jobs-list.page.ts
│   └── job-details.page.ts
└── tests/
    └── job-lifecycle.spec.ts   # Happy-path: create → schedule → start → complete
```

## Prerequisites

1. Backend running on `http://localhost:5000` with EF migrations applied.
2. Frontend prod build available (`npm run build`).
3. `DEFAULT_ORG_ID` matching an existing organization in the database.

## Local run

```powershell
npx playwright install --with-deps chromium
npm run e2e
```

`playwright.config.ts` will start `npm run start` automatically and tear it down after the run.

Set `PLAYWRIGHT_SKIP_WEBSERVER=1` to point at an already-running server (useful when debugging).

## CI

See `Infra/github-workflows/e2e.yml`.

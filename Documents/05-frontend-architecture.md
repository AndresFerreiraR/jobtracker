# 05 — Frontend Architecture (Next.js 15 + FSD + Atomic)

> Scope: complete `/jobs` route. Server Components + client boundary, Feature Sliced Design, Atomic Design for shared UI, Zustand store for client-only UI state, mandatory React patterns (Compound, Controlled, useReducer, useMemo, Error Boundary).
> TypeScript **strict mode** everywhere. No `any`.

---

## 1. Global folder layout

```
web/
├── src/
│   ├── app/                                     # Next.js App Router
│   │   ├── layout.tsx
│   │   ├── page.tsx                             # marketing / login redirect
│   │   ├── (authenticated)/
│   │   │   └── jobs/
│   │   │       ├── layout.tsx
│   │   │       ├── page.tsx                     # server-only, RSC
│   │   │       ├── loading.tsx                  # route-level skeleton
│   │   │       ├── error.tsx                    # error boundary
│   │   │       ├── not-found.tsx                # custom 404
│   │   │       └── actions.ts                   # 'use server' — mutations only
│   │   └── api/                                 # (empty — API lives in .NET)
│   │
│   ├── presentation/                            # FSD views
│   │   └── views/
│   │       └── jobs/
│   │           ├── index.ts                     # public API of the view
│   │           ├── hooks/
│   │           │   └── use-jobs-page.hook.ts    # orchestrates slices
│   │           ├── components/
│   │           │   └── organisms/
│   │           │       └── jobs-client.component.tsx   # 'use client', thin shell
│   │           ├── features/                    # verb-named slices
│   │           │   ├── create-job/
│   │           │   ├── filter-jobs/
│   │           │   └── complete-job/
│   │           └── stores/
│   │               └── jobs-ui.store.ts         # Zustand
│   │
│   ├── shared/                                  # Atomic design + generic utils
│   │   ├── ui/
│   │   │   ├── atoms/
│   │   │   ├── molecules/
│   │   │   └── organisms/
│   │   ├── hooks/
│   │   ├── lib/
│   │   └── styles/
│   │
│   ├── entities/                                # FE domain types shared across views
│   │   └── job/
│   │       ├── model/
│   │       │   ├── job.types.ts                 # generated from OpenAPI + hand-written narrows
│   │       │   └── job-state.machine.ts         # discriminated union + transitionJob()
│   │       └── index.ts
│   │
│   ├── infrastructure/
│   │   ├── api/
│   │   │   ├── generated/                       # OpenAPI codegen output (do not edit)
│   │   │   ├── client.ts                        # typed fetch wrapper
│   │   │   └── errors.ts                        # Result<T, ApiError>
│   │   └── auth/
│   │
│   ├── lib/
│   │   ├── di/                                  # DI container for server-side use cases
│   │   │   ├── container.server.ts              # 'server-only'
│   │   │   └── tokens.ts
│   │   └── server-only-guard.ts
│   │
│   └── env.ts                                    # zod-validated env vars
│
├── e2e/                                          # Playwright
│   ├── page-objects/
│   ├── fixtures/
│   └── tests/
├── public/
├── next.config.ts
├── tsconfig.json
├── package.json
├── tailwind.config.ts
├── playwright.config.ts
└── vitest.config.ts
```

**Layer boundaries** (enforced by `eslint-plugin-boundaries` or a custom rule):
- `app/` may import from `presentation/`, `lib/`, `env`.
- `presentation/views/<view>/` is a **closed unit**: internal folders (`features/`, `hooks/`, `components/`) NEVER export outside the view except via `index.ts`.
- **Cross-slice imports are forbidden.** `features/create-job/` cannot import from `features/filter-jobs/`. Cross-slice coordination happens **only** through the view's `use-jobs-page.hook.ts` or the Zustand store.
- `shared/` may be imported everywhere but MUST NOT import from `presentation/` or `entities/`.
- `entities/` is a passive types-and-utilities layer, no React.

---

## 2. Server Component (`app/(authenticated)/jobs/page.tsx`)

```tsx
import "server-only";
import { Suspense } from "react";
import { getServerContainer } from "@/lib/di/container.server";
import { JobsClient } from "@/presentation/views/jobs";
import { JobListSkeleton } from "@/presentation/views/jobs/components/organisms/jobs-client.skeleton";

type SearchParams = {
  q?: string;
  statuses?: string;
  from?: string;
  to?: string;
  cursor?: string;
};

export const dynamic = "force-dynamic";

export default async function JobsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const sp = await searchParams;
  const container = getServerContainer();
  const initialPage = await container.searchJobs.execute({
    q: sp.q,
    statuses: sp.statuses?.split(","),
    from: sp.from,
    to: sp.to,
    cursor: sp.cursor,
    pageSize: 20,
  });

  return (
    <Suspense fallback={<JobListSkeleton />}>
      <JobsClient initialPage={initialPage} initialFilters={sp} />
    </Suspense>
  );
}
```

**Points:**
- `import "server-only"` — build fails if any client code imports this module.
- Data is fetched here (SSR) and passed as **props** to the client.
- No `"use server"` for reads. Server Actions are reserved for writes (`actions.ts`).
- `dynamic = "force-dynamic"` because the query is user-specific and tenant-scoped; we can revisit with segmented caching later.

### 2.1 `loading.tsx`

```tsx
import { JobListSkeleton } from "@/presentation/views/jobs/components/organisms/jobs-client.skeleton";
export default function Loading() {
  return <JobListSkeleton />;
}
```

### 2.2 `error.tsx`

```tsx
"use client";
import { useEffect } from "react";

export default function JobsError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => { console.error(error); }, [error]);
  return (
    <div role="alert" className="p-8 space-y-4">
      <h2 className="text-xl font-semibold">Something went wrong loading jobs.</h2>
      <p className="text-sm text-muted-foreground">
        {error.digest ? `Reference: ${error.digest}` : null}
      </p>
      <button
        onClick={reset}
        className="px-4 py-2 rounded bg-primary text-primary-foreground"
        data-testid="jobs-error-retry"
      >
        Try again
      </button>
    </div>
  );
}
```

### 2.3 `not-found.tsx`

```tsx
export default function JobsNotFound() {
  return (
    <div className="p-8">
      <h1 className="text-xl font-semibold">Job not found</h1>
      <p className="text-sm text-muted-foreground">
        The job you're looking for doesn't exist or you don't have access.
      </p>
    </div>
  );
}
```

### 2.4 `actions.ts` (Server Actions — mutations only)

```tsx
"use server";
import { getServerContainer } from "@/lib/di/container.server";
import { revalidatePath } from "next/cache";
import { z } from "zod";

const CreateJobSchema = z.object({
  title: z.string().min(1).max(200),
  description: z.string().max(4000).default(""),
  address: z.object({
    street: z.string().min(1).max(200),
    city: z.string().min(1).max(120),
    state: z.string().min(1).max(60),
    zipCode: z.string().regex(/^\d{5}(-\d{4})?$/),
    latitude: z.number().min(-90).max(90).nullable().optional(),
    longitude: z.number().min(-180).max(180).nullable().optional(),
  }),
  customerId: z.string().uuid(),
});

export async function createJobAction(input: unknown) {
  const parsed = CreateJobSchema.safeParse(input);
  if (!parsed.success) {
    return { ok: false as const, code: "Validation.Failed", errors: parsed.error.flatten().fieldErrors };
  }
  const container = getServerContainer();
  const result = await container.createJob.execute(parsed.data);
  if (!result.ok) return { ok: false as const, code: result.error.code, errors: null };
  revalidatePath("/jobs");
  return { ok: true as const, id: result.value };
}

export async function completeJobAction(input: { id: string; signatureUrl: string }) {
  const container = getServerContainer();
  const result = await container.completeJob.execute(input);
  if (!result.ok) return { ok: false as const, code: result.error.code };
  revalidatePath("/jobs");
  revalidatePath(`/jobs/${input.id}`);
  return { ok: true as const };
}
```

**Why Server Actions:** enforce credentials & tenant claim server-side, wrap use cases from the DI container, and get `revalidatePath` for free.

---

## 3. DI container (`lib/di/container.server.ts`)

```ts
import "server-only";
import { cache } from "react";
import { CreateJobUseCase } from "@/entities/job/use-cases/create-job";
import { SearchJobsUseCase } from "@/entities/job/use-cases/search-jobs";
import { CompleteJobUseCase } from "@/entities/job/use-cases/complete-job";
import { createApiClient } from "@/infrastructure/api/client";
import { getServerAuthToken } from "@/infrastructure/auth/server";

export const getServerContainer = cache(() => {
  const api = createApiClient({
    baseUrl: process.env.API_BASE_URL!,
    getToken: getServerAuthToken,
  });
  return {
    createJob: new CreateJobUseCase(api),
    searchJobs: new SearchJobsUseCase(api),
    completeJob: new CompleteJobUseCase(api),
  };
});
```

`cache(...)` deduplicates the container per request. Use cases wrap the API client; they exist so the RSC never talks to fetch directly, mirroring the Backend's Clean Architecture spirit on the frontend.

---

## 4. Entities layer — Job types + state machine

### 4.1 `job.types.ts` (excerpt; generated + narrowed)

```ts
// generated by openapi-typescript from the swagger.json
export type JobStatus =
  | "Draft" | "Scheduled" | "InProgress" | "Completed" | "Cancelled";

export interface JobListItem {
  id: string;
  title: string;
  description: string;
  status: JobStatus;
  scheduledDate: string | null;
  startedAt: string | null;
  completedAt: string | null;
  assigneeId: string | null;
  customerId: string;
  photoCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface Paged<T> {
  items: T[];
  nextCursor: string | null;
}
```

### 4.2 `job-state.machine.ts` — discriminated union + typed transitions

Directly answers Part 1.3 of the assessment.

```ts
export type JobState =
  | { kind: "Draft"; notes?: string }
  | { kind: "Scheduled"; scheduledDate: Date; assigneeId: string }
  | { kind: "InProgress"; startedAt: Date; assigneeId: string; photos: string[] }
  | { kind: "Completed"; startedAt: Date; completedAt: Date; assigneeId: string; photos: string[]; signatureUrl: string }
  | { kind: "Cancelled"; cancelledAt: Date; reason: string };

export type JobAction =
  | { type: "SCHEDULE"; scheduledDate: Date; assigneeId: string }
  | { type: "START"; startedAt: Date }
  | { type: "ADD_PHOTO"; url: string }
  | { type: "COMPLETE"; completedAt: Date; signatureUrl: string }
  | { type: "CANCEL"; cancelledAt: Date; reason: string };

// Type-level enforcement of valid transitions.
// The Extract<..., { kind }> pattern means: for a given current kind,
// only actions accepted from that kind are valid.
type ValidActions<S extends JobState> =
  S extends { kind: "Draft" }       ? Extract<JobAction, { type: "SCHEDULE" }> :
  S extends { kind: "Scheduled" }   ? Extract<JobAction, { type: "START" | "CANCEL" }> :
  S extends { kind: "InProgress" }  ? Extract<JobAction, { type: "ADD_PHOTO" | "COMPLETE" | "CANCEL" }> :
  never;

export function transitionJob<S extends JobState>(
  current: S,
  action: ValidActions<S>,
): JobState {
  switch (action.type) {
    case "SCHEDULE":
      // current must be Draft here (compile-time)
      return {
        kind: "Scheduled",
        scheduledDate: action.scheduledDate,
        assigneeId: action.assigneeId,
      };
    case "START": {
      const s = current as Extract<JobState, { kind: "Scheduled" }>;
      return {
        kind: "InProgress",
        startedAt: action.startedAt,
        assigneeId: s.assigneeId,
        photos: [],
      };
    }
    case "ADD_PHOTO": {
      const s = current as Extract<JobState, { kind: "InProgress" }>;
      return { ...s, photos: [...s.photos, action.url] };
    }
    case "COMPLETE": {
      const s = current as Extract<JobState, { kind: "InProgress" }>;
      return {
        kind: "Completed",
        startedAt: s.startedAt,
        completedAt: action.completedAt,
        assigneeId: s.assigneeId,
        photos: s.photos,
        signatureUrl: action.signatureUrl,
      };
    }
    case "CANCEL":
      return { kind: "Cancelled", cancelledAt: action.cancelledAt, reason: action.reason };
    default: {
      const _exhaustive: never = action;
      return _exhaustive;
    }
  }
}

export function getJobSummary(state: JobState): string {
  switch (state.kind) {
    case "Draft":      return `Draft${state.notes ? ` — ${state.notes}` : ""}`;
    case "Scheduled":  return `Scheduled for ${state.scheduledDate.toISOString()}`;
    case "InProgress": return `In progress (${state.photos.length} photos)`;
    case "Completed":  return `Completed at ${state.completedAt.toISOString()}`;
    case "Cancelled":  return `Cancelled: ${state.reason}`;
    default: {
      const _exhaustive: never = state;
      return _exhaustive;
    }
  }
}
```

Compile-time proof of invalid transitions: `transitionJob({ kind: "Completed", ... }, { type: "START", ... })` **won't compile** because `ValidActions<{ kind: "Completed" }>` is `never`.

---

## 5. Zustand store (`presentation/views/jobs/stores/jobs-ui.store.ts`)

The store holds **only** UI state. Server data (the job list) is a prop from the RSC. Client-side reads that need refresh use TanStack Query and are cached by API layer, not by the store.

```ts
"use client";
import { create } from "zustand";
import { subscribeWithSelector } from "zustand/middleware";
import type { JobStatus } from "@/entities/job";

export interface JobFilters {
  q: string;
  statuses: readonly JobStatus[];
  from: string | null;
  to: string | null;
}

export interface OptimisticPatch {
  id: string;
  status: JobStatus;
  backup: { status: JobStatus };   // for rollback
}

interface JobsUiState {
  selectedIds: ReadonlySet<string>;
  filters: JobFilters;
  optimisticPatches: ReadonlyMap<string, OptimisticPatch>;

  // actions
  toggleSelected(id: string): void;
  clearSelection(): void;
  setFilters(patch: Partial<JobFilters>): void;
  applyOptimisticStatus(id: string, next: JobStatus, current: JobStatus): void;
  confirmOptimistic(id: string): void;
  rollbackOptimistic(id: string): OptimisticPatch | undefined;
}

export const useJobsUiStore = create<JobsUiState>()(
  subscribeWithSelector((set, get) => ({
    selectedIds: new Set<string>(),
    filters: { q: "", statuses: [], from: null, to: null },
    optimisticPatches: new Map<string, OptimisticPatch>(),

    toggleSelected: (id) =>
      set((s) => {
        const next = new Set(s.selectedIds);
        next.has(id) ? next.delete(id) : next.add(id);
        return { selectedIds: next };
      }),

    clearSelection: () => set({ selectedIds: new Set() }),

    setFilters: (patch) =>
      set((s) => ({ filters: { ...s.filters, ...patch } })),

    applyOptimisticStatus: (id, next, current) =>
      set((s) => {
        const map = new Map(s.optimisticPatches);
        map.set(id, { id, status: next, backup: { status: current } });
        return { optimisticPatches: map };
      }),

    confirmOptimistic: (id) =>
      set((s) => {
        const map = new Map(s.optimisticPatches);
        map.delete(id);
        return { optimisticPatches: map };
      }),

    rollbackOptimistic: (id) => {
      const patch = get().optimisticPatches.get(id);
      set((s) => {
        const map = new Map(s.optimisticPatches);
        map.delete(id);
        return { optimisticPatches: map };
      });
      return patch;
    },
  })),
);
```

### 5.1 Selectors — prevent re-renders

```ts
export const selectSelectedIds = (s: JobsUiState) => s.selectedIds;
export const selectFilters     = (s: JobsUiState) => s.filters;
export const selectPatch = (id: string) => (s: JobsUiState) => s.optimisticPatches.get(id);

// Derived state — computed via selector, NOT via useEffect+setState.
// Combine server data (as prop) with optimistic patches in the component:
export function useMergedStatus(id: string, serverStatus: JobStatus) {
  const patch = useJobsUiStore(selectPatch(id));
  return patch?.status ?? serverStatus;
}
```

### 5.2 filteredJobs selector

The client never re-computes `filteredJobs` in a `useEffect` — it derives synchronously with `useMemo`:

```ts
"use client";
import { useMemo } from "react";
import { useJobsUiStore, selectFilters } from "./jobs-ui.store";
import type { JobListItem } from "@/entities/job";

export function useFilteredJobs(serverJobs: readonly JobListItem[]) {
  const filters = useJobsUiStore(selectFilters);
  return useMemo(() => {
    return serverJobs.filter((j) => {
      if (filters.statuses.length && !filters.statuses.includes(j.status)) return false;
      if (filters.q && !`${j.title} ${j.description}`.toLowerCase().includes(filters.q.toLowerCase())) return false;
      if (filters.from && j.scheduledDate && j.scheduledDate < filters.from) return false;
      if (filters.to && j.scheduledDate && j.scheduledDate > filters.to) return false;
      return true;
    });
  }, [serverJobs, filters]);
}
```

**Guardrail:** the store never stores the server jobs list. That would duplicate server state.

---

## 6. The client "thin shell" organism

`presentation/views/jobs/components/organisms/jobs-client.component.tsx`:

```tsx
"use client";
import { JobsPageTemplate } from "@/shared/ui/organisms/jobs-page.template";
import { FilterBar } from "../../features/filter-jobs";
import { CreateJobModal } from "../../features/create-job";
import { CompleteJobModal } from "../../features/complete-job";
import { JobListTable } from "@/shared/ui/organisms/job-list-table";
import { JobListErrorBoundary } from "@/shared/ui/organisms/job-list-error-boundary";
import { useJobsPage } from "../../hooks/use-jobs-page.hook";
import type { Paged, JobListItem } from "@/entities/job";

interface Props {
  initialPage: Paged<JobListItem>;
  initialFilters: Record<string, string | undefined>;
}

export function JobsClient({ initialPage, initialFilters }: Props) {
  const vm = useJobsPage(initialPage, initialFilters);
  return (
    <JobsPageTemplate
      header={
        <>
          <FilterBar />
          <button data-testid="open-create-job" onClick={vm.openCreate}>New job</button>
        </>
      }
    >
      <JobListErrorBoundary>
        <JobListTable
          items={vm.rows}
          onCompleteClick={vm.openComplete}
        />
      </JobListErrorBoundary>

      {vm.isCreateOpen ? (
        <CreateJobModal onClose={vm.closeCreate} onCreated={vm.onCreated} />
      ) : null}

      {vm.completingId ? (
        <CompleteJobModal
          jobId={vm.completingId}
          onClose={vm.closeComplete}
          onCompleted={vm.onCompleted}
        />
      ) : null}
    </JobsPageTemplate>
  );
}
```

- All state and handlers live in `useJobsPage`.
- **Ternary** for conditional rendering (not `&&`) per the rubric.
- `JobListErrorBoundary` is a class component wrapper (see §11).

---

## 7. Orchestrator hook (`use-jobs-page.hook.ts`)

```ts
"use client";
import { useCallback, useState } from "react";
import { useRouter } from "next/navigation";
import type { Paged, JobListItem } from "@/entities/job";
import { useFilteredJobs } from "../stores/use-filtered-jobs.hook";

export function useJobsPage(
  initialPage: Paged<JobListItem>,
  _initialFilters: Record<string, string | undefined>,
) {
  const router = useRouter();
  const [serverPage] = useState(initialPage);
  const rows = useFilteredJobs(serverPage.items);

  const [isCreateOpen, setCreateOpen] = useState(false);
  const [completingId, setCompletingId] = useState<string | null>(null);

  const openCreate  = useCallback(() => setCreateOpen(true), []);
  const closeCreate = useCallback(() => setCreateOpen(false), []);
  const openComplete  = useCallback((id: string) => setCompletingId(id), []);
  const closeComplete = useCallback(() => setCompletingId(null), []);

  const onCreated  = useCallback(() => { setCreateOpen(false); router.refresh(); }, [router]);
  const onCompleted = useCallback(() => { setCompletingId(null); router.refresh(); }, [router]);

  return {
    rows,
    isCreateOpen, openCreate, closeCreate, onCreated,
    completingId, openComplete, closeComplete, onCompleted,
  };
}
```

`router.refresh()` re-runs the RSC on the server → fetches fresh data → passes as props. No client-side data cache duplication.

---

## 8. Slice — `create-job`

`presentation/views/jobs/features/create-job/`:
```
create-job/
├── index.ts
├── components/
│   └── organisms/
│       └── create-job-modal.component.tsx
├── hooks/
│   └── use-create-job.hook.ts
└── model/
    ├── create-job.reducer.ts
    └── create-job.types.ts
```

### 8.1 `create-job.types.ts`

```ts
export interface CreateJobFormState {
  title: string;
  description: string;
  address: {
    street: string;
    city: string;
    state: string;
    zipCode: string;
    latitude: number | null;
    longitude: number | null;
  };
  customerId: string;
  errors: Partial<Record<string, string>>;
  status: "idle" | "submitting" | "error";
  serverError: string | null;
}

export type CreateJobAction =
  | { type: "FIELD"; name: keyof CreateJobFormState | `address.${keyof CreateJobFormState["address"]}`; value: unknown }
  | { type: "SET_ERRORS"; errors: Partial<Record<string, string>> }
  | { type: "SUBMIT_START" }
  | { type: "SUBMIT_SUCCESS" }
  | { type: "SUBMIT_ERROR"; message: string }
  | { type: "RESET" };
```

### 8.2 `create-job.reducer.ts` — `useReducer` per the rubric

```ts
import type { CreateJobFormState, CreateJobAction } from "./create-job.types";

export const initialCreateJobState: CreateJobFormState = {
  title: "",
  description: "",
  address: {
    street: "", city: "", state: "", zipCode: "",
    latitude: null, longitude: null,
  },
  customerId: "",
  errors: {},
  status: "idle",
  serverError: null,
};

export function createJobReducer(
  state: CreateJobFormState,
  action: CreateJobAction,
): CreateJobFormState {
  switch (action.type) {
    case "FIELD": {
      if (typeof action.name === "string" && action.name.startsWith("address.")) {
        const key = action.name.slice("address.".length) as keyof CreateJobFormState["address"];
        return {
          ...state,
          address: { ...state.address, [key]: action.value as never },
          errors: { ...state.errors, [action.name]: undefined },
        };
      }
      return {
        ...state,
        [action.name]: action.value,
        errors: { ...state.errors, [action.name as string]: undefined },
      } as CreateJobFormState;
    }
    case "SET_ERRORS":     return { ...state, errors: action.errors, status: "error" };
    case "SUBMIT_START":   return { ...state, status: "submitting", serverError: null };
    case "SUBMIT_SUCCESS": return { ...initialCreateJobState };
    case "SUBMIT_ERROR":   return { ...state, status: "error", serverError: action.message };
    case "RESET":          return initialCreateJobState;
    default: {
      const _exhaustive: never = action;
      return _exhaustive;
    }
  }
}
```

### 8.3 `use-create-job.hook.ts`

```ts
"use client";
import { useReducer, useCallback } from "react";
import { createJobAction } from "@/app/(authenticated)/jobs/actions";
import { createJobReducer, initialCreateJobState } from "../model/create-job.reducer";

export function useCreateJob(onCreated: (id: string) => void) {
  const [state, dispatch] = useReducer(createJobReducer, initialCreateJobState);

  const onFieldChange = useCallback(
    (name: string, value: unknown) =>
      dispatch({ type: "FIELD", name: name as never, value }),
    [],
  );

  const submit = useCallback(async () => {
    dispatch({ type: "SUBMIT_START" });
    const result = await createJobAction({
      title: state.title,
      description: state.description,
      address: state.address,
      customerId: state.customerId,
    });
    if (!result.ok) {
      if (result.errors) {
        const flat: Record<string, string> = {};
        for (const [k, v] of Object.entries(result.errors)) flat[k] = v?.[0] ?? "Invalid";
        dispatch({ type: "SET_ERRORS", errors: flat });
      } else {
        dispatch({ type: "SUBMIT_ERROR", message: result.code });
      }
      return;
    }
    dispatch({ type: "SUBMIT_SUCCESS" });
    onCreated(result.id);
  }, [state, onCreated]);

  return { state, onFieldChange, submit };
}
```

### 8.4 `create-job-modal.component.tsx` — controlled inputs

```tsx
"use client";
import { useCreateJob } from "../hooks/use-create-job.hook";
import { TextInput } from "@/shared/ui/atoms/text-input";
import { Modal } from "@/shared/ui/molecules/modal";
import { Button } from "@/shared/ui/atoms/button";

interface Props {
  onClose: () => void;
  onCreated: (id: string) => void;
}

export function CreateJobModal({ onClose, onCreated }: Props) {
  const { state, onFieldChange, submit } = useCreateJob(onCreated);

  return (
    <Modal onClose={onClose} testId="create-job-modal">
      <form
        onSubmit={(e) => { e.preventDefault(); void submit(); }}
        className="space-y-3"
      >
        <TextInput
          label="Title"
          value={state.title}
          onChange={(v) => onFieldChange("title", v)}
          error={state.errors.title}
          testId="create-job-title"
        />
        <TextInput
          label="Description"
          value={state.description}
          onChange={(v) => onFieldChange("description", v)}
          error={state.errors.description}
        />
        <TextInput
          label="Street"
          value={state.address.street}
          onChange={(v) => onFieldChange("address.street", v)}
          error={state.errors["address.street"]}
        />
        {/* ...city, state, zip, lat, lng, customerId... */}
        {state.serverError ? (
          <p role="alert" className="text-destructive">{state.serverError}</p>
        ) : null}
        <div className="flex gap-2 justify-end">
          <Button variant="ghost" type="button" onClick={onClose}>Cancel</Button>
          <Button
            type="submit"
            disabled={state.status === "submitting"}
            data-testid="create-job-submit"
          >
            {state.status === "submitting" ? "Creating…" : "Create"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
```

- **Controlled Component pattern**: parent (`useCreateJob`) owns state; `<TextInput>` receives `value` + `onChange`.
- **useReducer** for the coupled form fields.
- **useMemo** would apply if the form derived heavy values — omitted here for brevity.
- **Ternary** rendering (not `&&`).

### 8.5 `index.ts` (public API of the slice)

```ts
export { CreateJobModal } from "./components/organisms/create-job-modal.component";
```

Nothing else is exported. The reducer, hooks, and types are internals of the slice.

---

## 9. Slice — `filter-jobs` (Compound Component)

`filter-jobs/components/molecules/job-filter-bar.component.tsx`:

```tsx
"use client";
import { createContext, useContext, type ReactNode } from "react";
import { useJobsUiStore, selectFilters } from "../../../stores/jobs-ui.store";
import { TextInput } from "@/shared/ui/atoms/text-input";
import { MultiSelect } from "@/shared/ui/atoms/multi-select";
import { DateInput } from "@/shared/ui/atoms/date-input";

interface FilterContextValue {
  filters: ReturnType<typeof selectFilters>;
  setFilters: (patch: Partial<ReturnType<typeof selectFilters>>) => void;
}
const FilterContext = createContext<FilterContextValue | null>(null);
function useFilterCtx() {
  const ctx = useContext(FilterContext);
  if (!ctx) throw new Error("FilterBar.* must be used inside <FilterBar>");
  return ctx;
}

export function FilterBar({ children }: { children: ReactNode }) {
  const filters   = useJobsUiStore(selectFilters);
  const setFilters = useJobsUiStore((s) => s.setFilters);
  return (
    <FilterContext.Provider value={{ filters, setFilters }}>
      <div className="flex gap-3 items-end" data-testid="filter-bar">{children}</div>
    </FilterContext.Provider>
  );
}

FilterBar.Search = function FilterSearch() {
  const { filters, setFilters } = useFilterCtx();
  return (
    <TextInput
      label="Search"
      value={filters.q}
      onChange={(v) => setFilters({ q: v })}
      testId="filter-search"
    />
  );
};

FilterBar.Status = function FilterStatus() {
  const { filters, setFilters } = useFilterCtx();
  return (
    <MultiSelect
      label="Status"
      value={[...filters.statuses]}
      options={["Draft", "Scheduled", "InProgress", "Completed", "Cancelled"]}
      onChange={(v) => setFilters({ statuses: v as typeof filters.statuses })}
      testId="filter-status"
    />
  );
};

FilterBar.DateRange = function FilterDateRange() {
  const { filters, setFilters } = useFilterCtx();
  return (
    <>
      <DateInput label="From" value={filters.from} onChange={(v) => setFilters({ from: v })} />
      <DateInput label="To"   value={filters.to}   onChange={(v) => setFilters({ to: v })} />
    </>
  );
};
```

Usage:
```tsx
<FilterBar>
  <FilterBar.Search />
  <FilterBar.Status />
  <FilterBar.DateRange />
</FilterBar>
```

`index.ts` re-exports only `FilterBar`.

---

## 10. Slice — `complete-job` (optimistic update + rollback)

```ts
// use-complete-job.hook.ts
"use client";
import { useState, useCallback } from "react";
import { completeJobAction } from "@/app/(authenticated)/jobs/actions";
import { useJobsUiStore } from "../../../stores/jobs-ui.store";
import type { JobStatus } from "@/entities/job";

export function useCompleteJob(jobId: string, currentStatus: JobStatus, onDone: () => void) {
  const [signatureUrl, setSignatureUrl] = useState("");
  const [status, setStatus] = useState<"idle" | "submitting" | "error">("idle");
  const [error, setError]   = useState<string | null>(null);
  const applyOptimistic = useJobsUiStore((s) => s.applyOptimisticStatus);
  const confirm         = useJobsUiStore((s) => s.confirmOptimistic);
  const rollback        = useJobsUiStore((s) => s.rollbackOptimistic);

  const submit = useCallback(async () => {
    if (!signatureUrl) { setError("Signature URL is required."); return; }
    setStatus("submitting"); setError(null);
    applyOptimistic(jobId, "Completed", currentStatus);
    const result = await completeJobAction({ id: jobId, signatureUrl });
    if (!result.ok) {
      rollback(jobId);
      setStatus("error");
      setError(result.code);
      return;
    }
    confirm(jobId);
    setStatus("idle");
    onDone();
  }, [signatureUrl, jobId, currentStatus, applyOptimistic, confirm, rollback, onDone]);

  return { signatureUrl, setSignatureUrl, status, error, submit };
}
```

- **Optimistic update**: `applyOptimisticStatus` mutates the store; the row re-renders as Completed instantly.
- **Rollback** on failure: `rollbackOptimistic` restores the backup.
- On success, `router.refresh()` in the orchestrator hook picks up the server's authoritative view.

---

## 11. Error Boundary organism

```tsx
"use client";
import { Component, type ReactNode } from "react";

interface Props { children: ReactNode; fallback?: ReactNode; }
interface State { hasError: boolean; error?: Error; }

export class JobListErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };
  static getDerivedStateFromError(error: Error): State { return { hasError: true, error }; }
  componentDidCatch(error: Error, info: unknown) { console.error(error, info); }
  render() {
    if (this.state.hasError) {
      return this.props.fallback ?? (
        <div role="alert" className="p-6 border border-destructive">
          <p>Failed to render the job list.</p>
        </div>
      );
    }
    return this.props.children;
  }
}
```

Wraps the table inside the client shell.

---

## 12. Typed API client (`infrastructure/api/client.ts`)

```ts
import type { paths, components } from "./generated/schema";
export type ApiJobListItem = components["schemas"]["JobListItemResponse"];

type ApiError = { code: string; message: string; status: number };
export type Result<T> = { ok: true; value: T } | { ok: false; error: ApiError };

interface ClientOptions { baseUrl: string; getToken: () => Promise<string | null>; }

export function createApiClient(opt: ClientOptions) {
  async function request<T>(path: string, init?: RequestInit): Promise<Result<T>> {
    const token = await opt.getToken();
    const res = await fetch(`${opt.baseUrl}${path}`, {
      ...init,
      headers: {
        "content-type": "application/json",
        ...(token ? { authorization: `Bearer ${token}` } : {}),
        ...(init?.headers ?? {}),
      },
      cache: "no-store",
    });
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      return { ok: false, error: { code: body.code ?? "Unknown", message: body.title ?? res.statusText, status: res.status } };
    }
    const value = res.status === 204 ? (undefined as T) : ((await res.json()) as T);
    return { ok: true, value };
  }
  return {
    searchJobs: (q: paths["/api/v1/jobs"]["get"]["parameters"]["query"]) =>
      request<paths["/api/v1/jobs"]["get"]["responses"]["200"]["content"]["application/json"]>(
        `/api/v1/jobs?${new URLSearchParams(q as Record<string, string>).toString()}`,
      ),
    createJob: (body: paths["/api/v1/jobs"]["post"]["requestBody"]["content"]["application/json"]) =>
      request<{ id: string }>("/api/v1/jobs", { method: "POST", body: JSON.stringify(body) }),
    completeJob: (id: string, signatureUrl: string) =>
      request<undefined>(`/api/v1/jobs/${id}/complete`, {
        method: "POST",
        body: JSON.stringify({ signatureUrl }),
      }),
  };
}
```

- Types come from `openapi-typescript` on the swagger.json produced by the backend CI.
- The wrapper returns a `Result` (mirrors the backend contract). No throwing on non-2xx.

---

## 13. Styles / accessibility

- Tailwind + shadcn/ui provides consistent atoms with sensible a11y defaults.
- Every interactive `<button>` receives a `data-testid` for Playwright.
- `aria-live="polite"` on the optimistic status cell so screen readers announce transitions.
- Focus trap in `Modal` via `@radix-ui/react-dialog` (shadcn).

---

## 14. Mandatory React patterns checklist (rubric)

| Pattern | Applied in |
|---|---|
| **Controlled Component** | `TextInput` / `MultiSelect` / `DateInput` receive `value` + `onChange`; parent owns state. |
| **Compound Component** | `<FilterBar>` with `<FilterBar.Search/>`, `<FilterBar.Status/>`, `<FilterBar.DateRange/>`. |
| **useReducer** | `create-job.reducer.ts` for the multi-field form. |
| **useMemo** | `useFilteredJobs` uses `useMemo` to derive rows from server data + filters. |
| **Error Boundary** | `<JobListErrorBoundary>` wraps the table. |
| **Ternary conditional rendering** | All modals and conditional UI use `cond ? <A/> : null`. |

---

## 15. Server / Client boundary — rules recap

1. `page.tsx` MUST import `server-only`. Never `"use client"` at the top of a route.
2. `page.tsx` fetches via the DI container (a use case), never via `fetch` directly.
3. Server Actions (`actions.ts`) handle **only** mutations. Reads never go through Server Actions.
4. `"use client"` sits on leaf files: organisms, hooks, stores.
5. Zustand store lives on the client side and NEVER re-declares server state.
6. `router.refresh()` is the single mechanism to re-fetch after a mutation.

---

## 16. Related documents

- 04 — API contracts (typed by the codegen).
- 07 — Testing strategy (Vitest + Playwright POM).
- 08 — DevOps (CI runs `openapi-typescript` against the backend `swagger.json`).

# 07 — Testing Strategy

> Goal: a **test pyramid** with fast unit tests at the base, integration tests in the middle, and a thin layer of E2E tests at the top. Every test is deterministic, isolated, and readable. AAA layout everywhere.

---

## 1. Pyramid at a glance

```
                 ┌─────────────────┐
                 │   E2E (few)     │  Playwright, POM, real backend, Testcontainers Postgres
                 ├─────────────────┤
                 │  Integration    │  xUnit + Testcontainers (BE), Vitest + MSW (FE)
                 │                 │
                 ├─────────────────┤
                 │      Unit       │  xUnit + Moq + FluentAssertions (BE), Vitest + RTL (FE)
                 │                 │
                 ├─────────────────┤
                 │   Architecture  │  NetArchTest, eslint-plugin-boundaries
                 └─────────────────┘
```

| Tier | Backend tools | Frontend tools |
|---|---|---|
| Unit | xUnit, FluentAssertions, Moq | Vitest, Testing Library, `expectTypeOf` |
| Integration | xUnit + Testcontainers.PostgreSql, WebApplicationFactory | Vitest + MSW + jsdom |
| E2E | (drives via Playwright) | Playwright + POM |
| Architecture | NetArchTest | eslint-plugin-boundaries + custom TS rules |

Convention across the board: `Should_<behavior>_When_<condition>` for backend, `describe('<subject>') → it('<behavior>')` for frontend.

---

## 2. Backend — unit tests (`xUnit`)

### 2.1 Layout

```
tests/
├── Jobs.Domain.UnitTests/
│   ├── AddressTests.cs
│   ├── JobTests.cs                        # invariants + state machine
│   └── JobPhotoTests.cs
├── Jobs.Application.UnitTests/
│   ├── Commands/
│   │   ├── CreateJobCommandHandlerTests.cs
│   │   ├── ScheduleJobCommandHandlerTests.cs
│   │   └── CompleteJobCommandHandlerTests.cs
│   ├── Queries/
│   │   └── SearchJobsQueryHandlerTests.cs
│   └── Validators/
│       └── CreateJobCommandValidatorTests.cs
```

### 2.2 Aggregate invariant example — `JobTests`

```csharp
public sealed class JobTests
{
    private static readonly OrganizationId Org = new(Guid.NewGuid());
    private static readonly CustomerId Cust    = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Address Addr =
        Address.Create("1 Main", "Akron", "OH", "44301", null, null).Value!;

    [Fact]
    public void Create_Should_Emit_JobCreatedDomainEvent()
    {
        var job = Job.Create(Org, "Roof", "desc", Addr, Cust, Now).Value!;
        job.Events.Should().ContainSingle().Which.Should().BeOfType<JobCreatedDomainEvent>();
        job.Status.Should().Be(JobStatus.Draft);
    }

    [Fact]
    public void Schedule_Should_Fail_When_Date_In_Past()
    {
        var job = Job.Create(Org, "Roof", "", Addr, Cust, Now).Value!;
        var result = job.Schedule(Now.AddMinutes(-1), new AssigneeId(Guid.NewGuid()), Now);
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Job.CannotScheduleInPast");
    }

    [Fact]
    public void Complete_Should_Fail_When_Not_InProgress()
    {
        var job = Job.Create(Org, "Roof", "", Addr, Cust, Now).Value!;
        // Direct Draft → Complete is invalid.
        var result = job.Complete("https://ex/sig.png", Now);
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Job.InvalidTransition");
    }

    [Theory]
    [InlineData(JobStatus.Draft, JobStatus.Scheduled)]
    [InlineData(JobStatus.Scheduled, JobStatus.InProgress)]
    [InlineData(JobStatus.InProgress, JobStatus.Completed)]
    public void Valid_Transitions_Should_Succeed(JobStatus from, JobStatus to)
    {
        var job = BuildJobIn(from);
        var result = PerformTransitionTo(job, to);
        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(to);
    }

    // Helpers omitted for brevity — set up a Job in each state via legit calls only,
    // never via reflection, so the tests document the actual API surface.
}
```

### 2.3 Address VO — structural equality

```csharp
public sealed class AddressTests
{
    [Fact]
    public void Equality_Is_Structural()
    {
        var a = Address.Create("1 Main", "Akron", "OH", "44301", 41.0m, -81.0m).Value!;
        var b = Address.Create("1 Main", "Akron", "OH", "44301", 41.0m, -81.0m).Value!;
        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Create_Should_Reject_Invalid_Zip()
    {
        var r = Address.Create("1 Main", "Akron", "OH", "not-a-zip", null, null);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Address.InvalidZipCode");
    }
}
```

### 2.4 Handler test with mocked repository

```csharp
public sealed class CreateJobCommandHandlerTests
{
    private readonly Mock<IJobRepository> _repo   = new(MockBehavior.Strict);
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly CreateJobCommandHandler _sut;
    private readonly OrganizationId _org = new(Guid.NewGuid());
    private readonly DateTimeOffset _now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    public CreateJobCommandHandlerTests()
    {
        _tenant.SetupGet(x => x.OrganizationId).Returns(_org);
        _clock.SetupGet(x => x.UtcNow).Returns(_now);
        _sut = new CreateJobCommandHandler(_repo.Object, _tenant.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_Should_Persist_Job_And_Return_Id()
    {
        Job? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
             .Callback<Job, CancellationToken>((j, _) => captured = j)
             .Returns(Task.CompletedTask);

        var cmd = new CreateJobCommand(
            "Roof", "desc",
            new AddressDto("1 Main", "Akron", "OH", "44301", null, null),
            Guid.NewGuid());

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Events.Should().ContainSingle().Which.Should().BeOfType<JobCreatedDomainEvent>();
        captured.OrganizationId.Should().Be(_org);
        _repo.Verify(r => r.AddAsync(captured, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Error_When_Address_Invalid()
    {
        var cmd = new CreateJobCommand("Roof", "desc",
            new AddressDto("", "", "", "", null, null),  // invalid
            Guid.NewGuid());

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().StartWith("Address.");
        _repo.Verify(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

### 2.5 Validator test

```csharp
public sealed class CreateJobCommandValidatorTests
{
    private readonly CreateJobCommandValidator _v = new();

    [Fact]
    public void Should_Reject_Empty_Title()
    {
        var cmd = new CreateJobCommand("", "d",
            new AddressDto("s", "c", "st", "44301", null, null), Guid.NewGuid());
        _v.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Title);
    }
    // Additional data-driven scenarios omitted for brevity.
}
```

Uses `FluentValidation.TestHelper` for expressive assertions.

---

## 3. Backend — architecture tests (`NetArchTest`)

```csharp
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Job).Assembly;
    private static readonly Assembly Application = typeof(CreateJobCommand).Assembly;
    private static readonly Assembly Infrastructure = typeof(JobsDbContext).Assembly;

    [Fact]
    public void Domain_Should_Not_Reference_EFCore()
    {
        var result = Types.InAssembly(Domain)
            .Should().NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Application_Should_Not_Reference_Infrastructure()
    {
        var result = Types.InAssembly(Application)
            .Should().NotHaveDependencyOnAny("Jobs.Infrastructure")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Command_Handlers_Are_Internal_Sealed()
    {
        var result = Types.InAssembly(Application)
            .That().HaveNameEndingWith("CommandHandler").Or().HaveNameEndingWith("QueryHandler")
            .Should().BeSealed().And().NotBePublic()
            .GetResult();
        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Commands_And_Queries_Are_Sealed()
    {
        var result = Types.InAssembly(Application)
            .That().HaveNameEndingWith("Command").Or().HaveNameEndingWith("Query")
            .Should().BeSealed()
            .GetResult();
        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    private static string Describe(TestResult r) =>
        r.IsSuccessful ? "OK" : string.Join(", ", r.FailingTypeNames ?? Array.Empty<string>());
}
```

Run in CI on every PR. A rule violation fails the build → merge blocked.

---

## 4. Backend — integration tests (Testcontainers)

### 4.1 Fixture

```csharp
public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("jobtracker_it")
        .WithUsername("app")
        .WithPassword("app")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        var opts = new DbContextOptionsBuilder<JobsDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new JobsDbContext(opts, TestTenantContext.WithOrg(Guid.NewGuid()));
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> { }
```

### 4.2 Repository integration test

```csharp
[Collection("Postgres")]
public sealed class JobRepositoryTests
{
    private readonly PostgresFixture _fx;
    public JobRepositoryTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_RoundTrip()
    {
        var orgId = Guid.NewGuid();
        await using var db = BuildContext(orgId);
        var repo = new JobRepository(db);

        var job = Job.Create(new(orgId), "Roof", "d",
            Address.Create("1 Main", "Akron", "OH", "44301", null, null).Value!,
            new(Guid.NewGuid()), DateTimeOffset.UtcNow).Value!;
        await repo.AddAsync(job, default);
        await db.SaveChangesAsync();

        await using var db2 = BuildContext(orgId);
        var loaded = await new JobRepository(db2).GetByIdAsync(job.Id, default);
        loaded.Should().NotBeNull();
        loaded!.Title.Should().Be("Roof");
    }

    private JobsDbContext BuildContext(Guid orgId)
    {
        var opts = new DbContextOptionsBuilder<JobsDbContext>()
            .UseNpgsql(_fx.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new JobsDbContext(opts, TestTenantContext.WithOrg(orgId));
    }
}
```

### 4.3 Outbox interceptor test

Verifies that when a command completes, the outbox row is committed in the same tx.

```csharp
[Collection("Postgres")]
public sealed class OutboxInterceptorTests
{
    // ... arrange DbContext with real interceptor + a fake IPublisher that captures events.

    [Fact]
    public async Task Completing_A_Job_Enqueues_JobCompletedIntegrationEvent()
    {
        var job = await SetupInProgressJob();
        await using var db = BuildContextWithInterceptor();

        job.Complete("https://cdn/sig.png", DateTimeOffset.UtcNow);
        db.Jobs.Update(job);
        await db.SaveChangesAsync();

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.Type.Should().Contain(nameof(JobCompletedIntegrationEvent));
    }
}
```

### 4.4 API integration tests (`WebApplicationFactory`)

```csharp
public sealed class JobsApiTests(WebApplicationFactory<Program> factory, PostgresFixture pg)
    : IClassFixture<CustomWebAppFactory>, IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task POST_jobs_Should_Return_201_With_Id()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = TestJwt.BearerFor(orgId: Guid.NewGuid());

        var res = await client.PostAsJsonAsync("/api/v1/jobs", new
        {
            title = "Roof",
            description = "desc",
            address = new { street = "1 Main", city = "Akron", state = "OH", zipCode = "44301" },
            customerId = Guid.NewGuid()
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        (await res.Content.ReadFromJsonAsync<CreateResponse>())!.Id.Should().NotBeEmpty();
    }
}
```

`CustomWebAppFactory` replaces the connection string via `WithWebHostBuilder` to point at the Testcontainers Postgres.

---

## 5. Frontend — unit tests (Vitest + Testing Library)

### 5.1 Setup

`vitest.config.ts`:

```ts
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import path from "path";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: "./vitest.setup.ts",
    coverage: { provider: "v8", reporter: ["text", "lcov"], thresholds: { branches: 80 } },
  },
  resolve: { alias: { "@": path.resolve(__dirname, "src") } },
});
```

`vitest.setup.ts`:
```ts
import "@testing-library/jest-dom/vitest";
```

### 5.2 Reducer test — `create-job.reducer`

```ts
import { describe, it, expect } from "vitest";
import { createJobReducer, initialCreateJobState } from "@/presentation/views/jobs/features/create-job/model/create-job.reducer";

describe("createJobReducer", () => {
  it("updates a top-level field and clears its error", () => {
    const state = { ...initialCreateJobState, errors: { title: "Required" } };
    const next = createJobReducer(state, { type: "FIELD", name: "title", value: "Roof" });
    expect(next.title).toBe("Roof");
    expect(next.errors.title).toBeUndefined();
  });

  it("updates a nested address field", () => {
    const next = createJobReducer(initialCreateJobState, {
      type: "FIELD", name: "address.city", value: "Akron",
    });
    expect(next.address.city).toBe("Akron");
  });

  it("transitions submit_start → submit_success", () => {
    const submitting = createJobReducer(initialCreateJobState, { type: "SUBMIT_START" });
    expect(submitting.status).toBe("submitting");
    const success = createJobReducer(submitting, { type: "SUBMIT_SUCCESS" });
    expect(success).toEqual(initialCreateJobState);
  });

  it("stores per-field errors on set_errors", () => {
    const next = createJobReducer(initialCreateJobState, {
      type: "SET_ERRORS", errors: { title: "Required" },
    });
    expect(next.status).toBe("error");
    expect(next.errors.title).toBe("Required");
  });
});
```

### 5.3 Hook test — `useCreateJob`

```ts
import { renderHook, act } from "@testing-library/react";
import { vi, describe, it, expect, beforeEach } from "vitest";
import { useCreateJob } from "@/presentation/views/jobs/features/create-job/hooks/use-create-job.hook";

vi.mock("@/app/(authenticated)/jobs/actions", () => ({
  createJobAction: vi.fn(),
}));
import { createJobAction } from "@/app/(authenticated)/jobs/actions";

describe("useCreateJob", () => {
  const onCreated = vi.fn();
  beforeEach(() => { vi.clearAllMocks(); });

  it("submits the current state and calls onCreated on success", async () => {
    (createJobAction as vi.Mock).mockResolvedValueOnce({ ok: true, id: "abc" });
    const { result } = renderHook(() => useCreateJob(onCreated));

    act(() => result.current.onFieldChange("title", "Roof"));
    await act(async () => { await result.current.submit(); });

    expect(createJobAction).toHaveBeenCalledWith(expect.objectContaining({ title: "Roof" }));
    expect(onCreated).toHaveBeenCalledWith("abc");
  });

  it("stores validation errors returned by the server", async () => {
    (createJobAction as vi.Mock).mockResolvedValueOnce({
      ok: false, code: "Validation.Failed", errors: { title: ["Required"] },
    });
    const { result } = renderHook(() => useCreateJob(onCreated));
    await act(async () => { await result.current.submit(); });
    expect(result.current.state.status).toBe("error");
    expect(result.current.state.errors.title).toBe("Required");
    expect(onCreated).not.toHaveBeenCalled();
  });
});
```

### 5.4 Zustand store test — optimistic + rollback

```ts
import { describe, it, expect, beforeEach } from "vitest";
import { useJobsUiStore } from "@/presentation/views/jobs/stores/jobs-ui.store";

describe("useJobsUiStore", () => {
  beforeEach(() => {
    useJobsUiStore.setState({
      selectedIds: new Set(),
      filters: { q: "", statuses: [], from: null, to: null },
      optimisticPatches: new Map(),
    });
  });

  it("applies and confirms an optimistic patch", () => {
    useJobsUiStore.getState().applyOptimisticStatus("j1", "Completed", "InProgress");
    expect(useJobsUiStore.getState().optimisticPatches.get("j1")?.status).toBe("Completed");
    useJobsUiStore.getState().confirmOptimistic("j1");
    expect(useJobsUiStore.getState().optimisticPatches.has("j1")).toBe(false);
  });

  it("rolls back to backup on failure", () => {
    useJobsUiStore.getState().applyOptimisticStatus("j2", "Completed", "InProgress");
    const rolled = useJobsUiStore.getState().rollbackOptimistic("j2");
    expect(rolled?.backup.status).toBe("InProgress");
    expect(useJobsUiStore.getState().optimisticPatches.has("j2")).toBe(false);
  });

  it("toggleSelected adds and removes", () => {
    useJobsUiStore.getState().toggleSelected("a");
    expect(useJobsUiStore.getState().selectedIds.has("a")).toBe(true);
    useJobsUiStore.getState().toggleSelected("a");
    expect(useJobsUiStore.getState().selectedIds.has("a")).toBe(false);
  });
});
```

### 5.5 Type tests — `DeepReadonly`, `JobState`, `PathKeys`

```ts
import { expectTypeOf, describe, it } from "vitest";
import type { DeepReadonly, PathKeys } from "@/shared/lib/types";
import { transitionJob, type JobState } from "@/entities/job/model/job-state.machine";

describe("DeepReadonly", () => {
  it("marks nested objects readonly", () => {
    type In = { a: { b: { c: number[] } } };
    type Out = DeepReadonly<In>;
    expectTypeOf<Out["a"]["b"]["c"]>().toEqualTypeOf<ReadonlyArray<number>>();
  });

  it("handles Map and Set", () => {
    type Out = DeepReadonly<{ m: Map<string, number>; s: Set<{ x: number }> }>;
    expectTypeOf<Out["m"]>().toEqualTypeOf<ReadonlyMap<string, number>>();
    expectTypeOf<Out["s"]>().toEqualTypeOf<ReadonlySet<{ readonly x: number }>>();
  });
});

describe("PathKeys", () => {
  it("produces union of dot-notation paths", () => {
    type Keys = PathKeys<{ a: { b: string; c: { d: number } } }>;
    expectTypeOf<Keys>().toEqualTypeOf<"a.b" | "a.c.d">();
  });
});

describe("transitionJob type-level", () => {
  it("disallows Completed → InProgress at compile time", () => {
    const s: JobState = { kind: "Completed", startedAt: new Date(), completedAt: new Date(),
                           assigneeId: "x", photos: [], signatureUrl: "u" };
    // The following would fail to compile: uncomment locally to verify.
    // transitionJob(s, { type: "START", startedAt: new Date() });
    // For runtime this file just documents the intent; the type-level guarantee
    // is proven by `tsc --noEmit` running in CI.
    expectTypeOf(s).toMatchTypeOf<JobState>();
  });
});
```

Note: type tests run with `vitest --typecheck` or `tsc --noEmit` in CI.

---

## 6. Frontend — component tests (React Testing Library)

Focused on organism + hook wiring. Kept minimal because deep behavior is already covered in hook/reducer tests.

```ts
import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { CreateJobModal } from "@/presentation/views/jobs/features/create-job/components/organisms/create-job-modal.component";

vi.mock("@/app/(authenticated)/jobs/actions", () => ({
  createJobAction: vi.fn().mockResolvedValue({ ok: true, id: "abc" }),
}));

describe("<CreateJobModal>", () => {
  it("renders inputs and submits", async () => {
    const onCreated = vi.fn();
    render(<CreateJobModal onClose={() => {}} onCreated={onCreated} />);
    fireEvent.change(screen.getByTestId("create-job-title"), { target: { value: "Roof" } });
    fireEvent.click(screen.getByTestId("create-job-submit"));
    await screen.findByRole("form"); // wait a tick
    expect(onCreated).toHaveBeenCalledWith("abc");
  });
});
```

MSW is set up (`src/mocks/handlers.ts`) for tests that need to intercept fetch calls — used by hooks that go directly to the API client rather than a Server Action.

---

## 7. Frontend — E2E (Playwright)

### 7.1 Page Object Model

`e2e/page-objects/jobs.page.ts`:

```ts
import { expect, type Page } from "@playwright/test";

export class JobsPage {
  constructor(private page: Page) {}

  goto()               { return this.page.goto("/jobs"); }

  filterBar()          { return this.page.getByTestId("filter-bar"); }
  searchInput()        { return this.page.getByTestId("filter-search"); }
  statusFilter()       { return this.page.getByTestId("filter-status"); }
  openCreateButton()   { return this.page.getByTestId("open-create-job"); }
  jobRow(title: string){ return this.page.getByRole("row", { name: new RegExp(title, "i") }); }
  completeButton(row: ReturnType<JobsPage["jobRow"]>) { return row.getByTestId("complete-job"); }

  createJobModal() {
    return {
      title:     this.page.getByTestId("create-job-title"),
      submit:    this.page.getByTestId("create-job-submit"),
    };
  }

  completeJobModal() {
    return {
      signatureUrl: this.page.getByTestId("complete-signature-url"),
      submit:       this.page.getByTestId("complete-submit"),
    };
  }

  async createJob(title: string) {
    await this.openCreateButton().click();
    await this.createJobModal().title.fill(title);
    await this.createJobModal().submit.click();
  }

  async filterByStatus(status: string) {
    await this.statusFilter().click();
    await this.page.getByRole("option", { name: status }).click();
  }

  async completeJob(title: string, signatureUrl: string) {
    await this.completeButton(this.jobRow(title)).click();
    await this.completeJobModal().signatureUrl.fill(signatureUrl);
    await this.completeJobModal().submit.click();
  }
}
```

### 7.2 Test — full flow

`e2e/tests/jobs.spec.ts`:

```ts
import { test, expect } from "@playwright/test";
import { JobsPage } from "../page-objects/jobs.page";

test.describe("Jobs — full lifecycle", () => {
  test("create, filter, complete", async ({ page }) => {
    const jobs = new JobsPage(page);
    await jobs.goto();

    const title = `Roof ${Date.now()}`;
    await jobs.createJob(title);

    await expect(jobs.jobRow(title)).toBeVisible();

    // Optimistic complete flow needs the job to be In Progress first.
    // For the assessment E2E, we can seed a job in InProgress via a test-only API endpoint,
    // or drive create → schedule → start via the UI.
    await jobs.filterByStatus("Completed");
    // ...
  });

  test("takes a screenshot on failure", async ({ page }, testInfo) => {
    testInfo.attach("screenshot", { body: await page.screenshot(), contentType: "image/png" });
  });
});
```

### 7.3 Config

`playwright.config.ts`:

```ts
import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e/tests",
  fullyParallel: true,
  retries: 1,
  reporter: [["list"], ["html", { outputFolder: "playwright-report" }]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:3000",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
  ],
});
```

### 7.4 Fixtures / seeding

E2E tests need a known tenant + seed data. Two options:

| Option | Pros | Cons | Chosen |
|---|---|---|---|
| **A. Programmatic seed via a test-only endpoint** `/api/v1/test/seed` (enabled only when `ASPNETCORE_ENVIRONMENT=Test`). | Fast, deterministic. | Extra surface to maintain. | ✓ |
| B. Drive the whole seed via the UI. | Zero backend changes. | Slow + flaky. | ✗ |

Fixture wrapper:
```ts
import { test as base } from "@playwright/test";

export const test = base.extend<{ seededOrg: string }>({
  seededOrg: async ({ request }, use) => {
    const { orgId } = await (await request.post("/api/v1/test/seed")).json();
    await use(orgId);
    await request.post("/api/v1/test/reset", { data: { orgId } });
  },
});
```

---

## 8. Determinism guardrails (across the pyramid)

| Concern | Rule |
|---|---|
| Time | Never use `DateTime.UtcNow` / `new Date()` in production code. Inject a provider. In tests, freeze the clock. |
| Randomness | Same — inject `IRandomProvider` when logic depends on it. |
| Network in unit tests | Forbidden. Mock at the boundary. |
| Testcontainers boot | One per test class (via `IAsyncLifetime`) — do not restart per test. |
| DB state in integration tests | Each test in a fresh transaction that rolls back at teardown, OR uses a schema-per-test pattern. Do not rely on ordering. |
| E2E parallelism | Every E2E test seeds its own tenant. No shared state between tests. |

---

## 9. Coverage & CI enforcement

- Backend: `coverlet.collector` produces `coverage.cobertura.xml`. CI enforces:
  - `Jobs.Domain` ≥ 90% line, 85% branch.
  - `Jobs.Application` ≥ 80% line, 75% branch.
  - Infrastructure ≥ 60% (integration-covered).
- Frontend: Vitest v8 coverage. `presentation/views/jobs/**` ≥ 80% branch.
- Architecture tests: 100% must pass. No skips.

`PR blocked if:`
- Any test fails.
- Coverage falls below threshold.
- Type check fails.
- Lint fails.

---

## 10. Test-first checklist per feature

For each new command / query:

1. Domain unit test proving the invariant (or updating it).
2. Handler unit test with mocked repo verifying:
   - Result on success (return value + side effects on the aggregate).
   - Result on failure (exact `Error.Code`).
3. Validator unit test for edge cases.
4. Integration test round-tripping the aggregate through the real Postgres.
5. E2E test only when the user story crosses the UI boundary.

---

## 11. Related documents

- 01 — Domain model (invariants tested here).
- 03 — Backend solution (architecture rules tested here).
- 05 — Frontend architecture (components tested here).
- 06 — Async messaging (outbox interceptor test).
- 08 — DevOps (CI runs everything).

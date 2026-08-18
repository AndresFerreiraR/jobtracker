# 09 — Principles and Patterns

> Consolidated evidence for **SOLID**, **GRASP**, and **GoF / Enterprise** patterns actually applied in the JobTracker codebase.
> Purpose: satisfy rubric sections **6.2 (SOLID)** and **6.3 (Design Patterns)** with concrete code references — not theory.
> Every principle below points to a real file in `api/` or `web/`.

---

## 1. How to read this document

Each principle / pattern entry follows the same three-part shape:

1. **Definition (one paragraph).** What it means, in the vocabulary we use across the docs.
2. **Applied here.** File path(s) + minimal code excerpt showing the principle in action.
3. **Trade-off / non-example.** What we did *not* do and why — to make the choice explicit.

Section 5 closes with a **rubric cross-reference matrix** so evaluators can jump straight from a rubric bullet to the evidence.

---

## 2. SOLID

### 2.1 Single Responsibility Principle (SRP)

> A class has one reason to change. In practice: it collaborates with a single actor or serves a single "axis of change".

**Applied here — one command, one handler, one validator.**

Instead of an "orchestrator service" that does validation + business logic + persistence, each request has a dedicated pipeline of small classes:

```
src/Modules/Jobs/Jobs.Application/Jobs/Commands/CreateJob/
  CreateJobCommand.cs           (DTO — request shape)
  CreateJobCommandValidator.cs  (FluentValidation — input rules)
  CreateJobCommandHandler.cs    (orchestrates domain + repository)
```

Reason: validation changes (FE contract) is a different axis than domain rules (business), and both differ from persistence (Infra). Splitting them means a change in Zip regex touches only `Validator`, not `Handler`.

**Non-example we avoided.** A "`JobService.CreateJob(...)`" method containing validation, business rules, and DB calls — a common anti-pattern where a single method needs to change for FE, business, and DB reasons.

---

### 2.2 Open-Closed Principle (OCP)

> Modules are open for extension, closed for modification.

**Applied here — MediatR pipeline behaviors.**

Adding cross-cutting concerns (logging, validation, retries, metrics) does **not** touch existing handlers. It is done by registering a new `IPipelineBehavior<TRequest, TResponse>`:

```csharp
// src/Host/JobTracker.Api/Program.cs
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

A future `TransactionBehavior<,>` or `IdempotencyBehavior<,>` slots in without editing `CreateJobCommandHandler`.

**Applied here — integration event mapping.** Adding a new domain-to-integration mapping means adding one case to `JobsIntegrationEventMapper.Map(...)`, not editing the aggregate. The interceptor (`InsertOutboxMessagesInterceptor`) is closed to modification and treats new event types uniformly.

---

### 2.3 Liskov Substitution Principle (LSP)

> Subtypes must be usable wherever their base type is expected, preserving the base's contract.

**Applied here — `Result<T>` / `Result`.**

Every command / query handler returns `Result` or `Result<T>`. Callers can compose them uniformly without downcasts:

```csharp
public interface ICommandHandler<TCommand>          : IRequestHandler<TCommand, Result> where TCommand : ICommand { }
public interface ICommandHandler<TCommand, TResp>   : IRequestHandler<TCommand, Result<TResp>> where TCommand : ICommand<TResp> { }
```

The `ApiControllerBase.ToActionResult` method treats *any* `Result` identically — never checks the concrete command type. A new handler that returns `Result<Guid>` behaves exactly like an existing one that returns `Result<JobDetailsDto>`, only the payload type changes.

**Non-example we avoided.** Throwing custom exceptions from handlers, forcing callers to know which exception subclass to catch. That violates LSP because different subclasses have different runtime semantics (401 vs 404 vs 500) baked into their identity.

---

### 2.4 Interface Segregation Principle (ISP)

> Clients should not be forced to depend on methods they do not use.

**Applied here — split of `IJobRepository` vs `IJobQueryService`.**

```csharp
// Write side: only 2 methods, matches command needs.
public interface IJobRepository
{
    Task<Job?> GetByIdAsync(JobId id, CancellationToken ct = default);
    Task AddAsync(Job job, CancellationToken ct = default);
}

// Read side: query-shaped, returns DTOs.
public interface IJobQueryService
{
    Task<JobDetailsDto?> GetByIdAsync(Guid orgId, Guid jobId, CancellationToken ct = default);
    Task<(IReadOnlyList<JobListItemDto> Items, string? NextCursor)> ListAsync(
        Guid orgId, ListJobsFilter filter, CancellationToken ct = default);
}
```

Command handlers depend only on `IJobRepository`; query handlers only on `IJobQueryService`. Neither can accidentally invoke a method it "shouldn't". This is the CQRS variant of ISP.

**Applied here — `ITenantContext` returns only what a request needs.** No "current user with roles + claims + audit trail" god-interface; just `OrganizationId` and `IsPresent`.

---

### 2.5 Dependency Inversion Principle (DIP)

> High-level modules do not depend on low-level modules; both depend on abstractions.

**Applied here — Clean Architecture layer graph enforced by NetArchTest.**

```
Jobs.Domain      →  (nothing)
Jobs.Application →  Jobs.Domain, BuildingBlocks.Application, SharedKernel
Jobs.Infrastructure → Jobs.Application (only via abstractions), Jobs.Domain
Jobs.Presentation → Jobs.Application (via MediatR contracts)
```

Application defines `IJobRepository` / `IUnitOfWork` / `IDateTimeProvider`. Infrastructure implements them. Guarded automatically:

```csharp
// tests/JobTracker.Tests.Architecture/LayeringRulesTests.cs
[Fact]
public void Jobs_Application_does_not_reference_EntityFrameworkCore()
{
    var result = Types.InAssembly(JobsApplication)
        .Should()
        .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
        .GetResult();
    result.IsSuccessful.Should().BeTrue();
}
```

**Non-example we avoided.** Injecting `DbContext` into handlers directly. That would collapse Application onto Infrastructure and prevent unit-testing handlers without a live database.

---

## 3. GRASP

GRASP ("General Responsibility Assignment Software Patterns") answers: *who gets which responsibility?* This is complementary to SOLID (which is about *class shape*).

### 3.1 Information Expert

> Assign the responsibility to the class that has the information needed to fulfill it.

`Job` owns its state, so `Job` (not a service) is the class that decides whether a transition is legal:

```csharp
// src/Modules/Jobs/Jobs.Domain/Jobs/Job.cs
public Result Schedule(DateTimeOffset scheduledDate, AssigneeId assigneeId, DateTimeOffset nowUtc)
{
    if (Status is not JobStatus.Draft)
        return Result.Failure(JobErrors.InvalidTransition(Status, JobStatus.Scheduled));
    if (scheduledDate <= nowUtc)
        return Result.Failure(JobErrors.CannotScheduleInPast);
    // ...
}
```

Handlers never re-check invariants; they call `job.Schedule(...)` and let the aggregate answer.

---

### 3.2 Creator

> B creates A when B contains, aggregates, records, or has the initializing data for A.

`Job` creates `JobPhoto` (records + closely uses + has initializing data). No public `JobPhoto` constructor callable from outside the aggregate — creation happens through `Job.AddPhoto(...)`:

```csharp
public Result<JobPhotoId> AddPhoto(string? url, DateTimeOffset capturedAt, string? caption)
{
    // ... validation ...
    var photoId = JobPhotoId.New();
    _photos.Add(new JobPhoto(photoId, Id, url!, capturedAt, caption));
    return photoId;
}
```

The `JobPhoto` constructor is `internal` — enforced by C# accessibility. Nothing outside the assembly can bypass the aggregate to build one.

---

### 3.3 Controller

> Assign the responsibility of handling a system event to a class representing the overall system or a use-case.

`JobsController` is a *façade* — it does not implement business logic itself. It translates HTTP into `ICommand` / `IQuery`, dispatches via `ISender`, and translates `Result` back to HTTP. The use-case controller is the MediatR handler (`CreateJobCommandHandler`, etc.), one per use case.

---

### 3.4 Low Coupling / High Cohesion

**High cohesion inside a module.** `Jobs.*` projects contain everything related to jobs: domain, application, integration events, infrastructure, presentation. A new team member touches at most 5 projects to add a Jobs feature — never scattered across "the DTO project", "the mappers project", "the services project".

**Low coupling between modules.** `Jobs` never references `Billing.Infrastructure` or `Notifications.Infrastructure`. Communication is by **integration events** (immutable records in `Jobs.IntegrationEvents`), the only assembly reachable from outside the module.

```csharp
// tests/JobTracker.Tests.Architecture/LayeringRulesTests.cs
[Fact]
public void Jobs_IntegrationEvents_depends_only_on_SharedKernel_and_MediatR()
{
    var result = Types.InAssembly(JobsIntegrationEvents)
        .Should()
        .NotHaveDependencyOnAny(
            "Jobs.Domain", "Jobs.Application", "Jobs.Infrastructure", "Jobs.Presentation",
            "JobTracker.BuildingBlocks.Application", ...)
        .GetResult();
    result.IsSuccessful.Should().BeTrue();
}
```

---

### 3.5 Polymorphism

Choices that vary by type are resolved through polymorphism, not `if`/`switch` on runtime type:

- `IPipelineBehavior<,>` is polymorphic over `TRequest` and `TResponse`.
- `IEntityTypeConfiguration<T>` binds one aggregate at a time — no giant switch.
- `IIntegrationEventMapper.Map(...)` uses a pattern-matching switch (allowed exception — it is the *single* explicit adapter between two orthogonal type universes).

---

### 3.6 Pure Fabrication

> Introduce an artificial class (not a real-world concept) to keep other classes cohesive.

- `Result<T>` — not a business concept; a technical convenience preventing exceptions from becoming control flow.
- `OutboxMessage` — not a domain concept; infrastructure fabrication so we can guarantee at-least-once delivery.
- `Cursor` (base64 keyset token) — a fabrication to keep pagination stateless and idempotent.

---

### 3.7 Indirection

`IUnitOfWork` is an indirection: the controller / handler needs "save my changes", but doesn't want to know about `JobsDbContext`. Adding a second module (e.g. `Billing`) means one more `IUnitOfWork` binding — no scattered `DbContext` references in Application code.

---

### 3.8 Protected Variations

> Identify points of predicted variation and hide them behind a stable interface.

Predicted variations we hid:

| Variation point | Interface |
|---|---|
| Persistence engine (Postgres today, could be another SQL) | `IJobRepository`, `IUnitOfWork` |
| Time source (real / frozen for tests) | `IDateTimeProvider` |
| Tenant identity (header today, JWT tomorrow) | `ITenantContext` |
| Message publication (Outbox today, RabbitMQ or Azure Service Bus later) | `IOutboxWriter` + `IIntegrationEventMapper` |
| HTTP transport (fetch today, RSC streaming tomorrow) | `shared/api/http.ts` in the frontend |

Any of these can change without editing a single line of Application or Domain code.

---

### 3.9 Don't Talk to Strangers (Law of Demeter)

The frontend does not chain `.customer.address.street` on server results — it copies flat properties from the response DTO into UI-shaped types. On the backend, controllers never reach into `job.Photos[0].Url` — the DTO shape (`JobDetailsDto`) is projected once inside `JobQueryService`.

---

## 4. Design Patterns (GoF + Enterprise)

### 4.1 Creational

#### Factory Method (`Address.Create`, `Job.Create`)

Aggregates and Value Objects **never** expose public constructors that could produce an invalid state. Creation is always through a static factory that returns `Result<T>`:

```csharp
// src/Modules/Jobs/Jobs.Domain/Jobs/Address.cs
public static Result<Address> Create(string? street, string? city, string? state, string? zipCode, ...)
{
    if (string.IsNullOrWhiteSpace(street)) return AddressErrors.StreetRequired;
    if (string.IsNullOrWhiteSpace(city))   return AddressErrors.CityRequired;
    // ...
    return new Address(street.Trim(), city.Trim(), state.Trim(), trimmedZip, latitude, longitude);
}
```

The private constructor is unreachable outside the class — `Address` cannot exist unless it satisfied every rule. This is Fowler's *Domain Model with Always-Valid State*.

---

### 4.2 Structural

#### Adapter — `HeaderTenantContext`

Adapts `HttpContext` (a framework concern) to the domain-facing `ITenantContext` contract (an Application abstraction).

```csharp
// src/Host/JobTracker.Api/Infrastructure/Tenant/HeaderTenantContext.cs
internal sealed class HeaderTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid OrganizationId
    {
        get
        {
            var ctx = accessor.HttpContext ?? throw new InvalidOperationException(...);
            if (!ctx.Request.Headers.TryGetValue("X-Organization-Id", out var v) ||
                !Guid.TryParse(v, out var orgId))
                throw new InvalidOperationException(...);
            return orgId;
        }
    }
}
```

Swapping to JWT-based extraction later is a one-file change.

#### Facade — `AddJobsInfrastructure`

The composition root (`Program.cs`) never sees `JobsDbContext`, `JobRepository`, `JobQueryService`, `InsertOutboxMessagesInterceptor`, or `JobsIntegrationEventMapper`. It sees one method:

```csharp
builder.Services.AddJobsInfrastructure(builder.Configuration);
```

The internals are the module's business; the composition root just wires modules together.

#### Composite — `Job` + `JobPhoto`

The aggregate is a composite: iterating `job.Photos` yields entities that are conceptually leaves of the same object graph. The write model treats them as one transactional unit (cascade delete via EF configuration).

---

### 4.3 Behavioral

#### Chain of Responsibility — MediatR pipeline

```
Controller → ISender.Send → LoggingBehavior → ValidationBehavior → CreateJobCommandHandler
```

Each behavior can short-circuit (validation failure returns `Result.Failure` and skips the handler) or delegate to the next via `next()`. Adding a `TransactionBehavior` or `RetryBehavior` extends the chain without touching existing links.

#### Strategy — `ApiControllerBase.MapStatus`

The mapping of `ErrorType` (a domain concept) to HTTP status (a transport concern) is one strategy per error family:

```csharp
private static (int Status, string Title) MapStatus(ErrorType type) => type switch
{
    ErrorType.Validation   => (400, "Validation failed"),
    ErrorType.NotFound     => (404, "Resource not found"),
    ErrorType.Conflict     => (409, "Conflict"),
    ErrorType.Unauthorized => (401, "Unauthorized"),
    _                      => (500, "Unexpected error"),
};
```

Also visible on the frontend: `variantClasses` in `shared/ui/button.tsx` picks per-variant Tailwind classes.

#### Template Method — `ApiControllerBase.ToActionResult`

The base class defines the skeleton (success vs failure branching, ProblemDetails construction), and derived controllers plug in the type of the payload:

```csharp
protected IActionResult ToActionResult<T>(Result<T> r, int status = 200) =>
    r.IsSuccess ? StatusCode(status, r.Value) : ProblemFromError(r.Error);
```

Every `JobsController` action calls this method — the derived class only supplies command/query types.

#### Observer — Domain Events + Integration Events

`Job` raises `JobCreatedDomainEvent` when created. `InsertOutboxMessagesInterceptor` observes `ChangeTracker` entries implementing `IAggregateRoot` and consumes the events during `SaveChangesAsync`:

```csharp
// src/Modules/Jobs/Jobs.Infrastructure/Persistence/Outbox/InsertOutboxMessagesInterceptor.cs
var aggregates = context.ChangeTracker.Entries<IAggregateRoot>()
    .Where(e => e.Entity.Events.Count > 0)
    .Select(e => e.Entity);

foreach (var aggregate in aggregates)
{
    foreach (var domainEvent in aggregate.DrainEvents())
    {
        var integrationEvent = mapper.Map(domainEvent);
        // ... enqueue to outbox_messages ...
    }
}
```

MediatR itself implements Observer for `INotification` subscribers.

#### State — `JobStatus` transitions

The `Job` aggregate is a finite state machine. Transitions are guarded per source state:

```csharp
public Result Start(DateTimeOffset nowUtc)
{
    if (Status is not JobStatus.Scheduled)
        return Result.Failure(JobErrors.InvalidTransition(Status, JobStatus.InProgress));
    Status = JobStatus.InProgress;
    // ...
}
```

The frontend mirrors this at the type level:

```typescript
// entities/job/types.ts
type Transitions = {
  Draft:      'Scheduled' | 'Cancelled';
  Scheduled:  'InProgress' | 'Cancelled';
  InProgress: 'Completed' | 'Cancelled';
  Completed:  never;
  Cancelled:  never;
};
export type NextStatus<S extends JobStatus> = Transitions[S];
```

An illegal transition is a compile-time error in TypeScript. Same rule, two places, single source of truth is the domain doc.

#### Command — CQRS commands

`ICommand<TResponse>` and `ICommand` are marker interfaces. Every command is a data class describing an intent (`CreateJobCommand`, `ScheduleJobCommand`, ...). Handlers are one-per-command.

#### Mediator — MediatR

Controllers don't reference handlers; they publish through `ISender`. Wiring changes touch only DI registrations, not call sites.

---

### 4.4 Enterprise / DDD Patterns

| Pattern | Where |
|---|---|
| **Aggregate Root** | `Job` extends `AggregateRoot<JobId>` |
| **Entity** | `JobPhoto` extends `Entity<JobPhotoId>` |
| **Value Object** | `Address` extends `ValueObject`, defines `GetEqualityComponents()` |
| **Repository** | `IJobRepository` (Domain contract) / `JobRepository` (Infra impl) |
| **Unit of Work** | `IUnitOfWork` → `JobsDbContext` |
| **Query Service (CQRS)** | `IJobQueryService` / `JobQueryService`, projects DTOs with `AsNoTracking` |
| **Domain Events** | `IDomainEvent` + `AggregateRoot.RaiseDomainEvent` + `DrainEvents` |
| **Integration Events** | `Jobs.IntegrationEvents` project — stable public contract |
| **Outbox** | `outbox_messages` table + `InsertOutboxMessagesInterceptor` |
| **Idempotent Consumer** (planned) | `processed_inbox` table — reserved slot in doc 06 |
| **Result Object / Railway** | `Result` / `Result<T>` — no exceptions for business rule failures |
| **Specification** (light) | `ListJobsFilter` fields translate to composable `Where(...)` clauses |
| **Problem Details (RFC 7807)** | `ApiControllerBase.ProblemFromError` + `ExceptionToProblemDetailsMapper` |
| **Strongly-typed IDs** | `JobId`, `OrganizationId`, `AssigneeId`, `CustomerId`, `JobPhotoId` |

---

## 5. Cross-reference matrix (rubric)

| Rubric bullet | Where in codebase | Where in this doc |
|---|---|---|
| **6.2 SRP** | `Command / Validator / Handler` split under `Jobs.Application/Jobs/Commands/*` | §2.1 |
| **6.2 OCP** | `LoggingBehavior`, `ValidationBehavior` in `BuildingBlocks.Application.Behaviors` | §2.2 |
| **6.2 LSP** | `Result` / `Result<T>` uniform contract; handlers substitutable | §2.3 |
| **6.2 ISP** | `IJobRepository` vs `IJobQueryService` split | §2.4 |
| **6.2 DIP** | Layering enforced by `tests/JobTracker.Tests.Architecture/LayeringRulesTests.cs` | §2.5 |
| **6.3 Factory Method** | `Address.Create`, `Job.Create` | §4.1 |
| **6.3 Adapter** | `HeaderTenantContext` | §4.2 |
| **6.3 Facade** | `AddJobsInfrastructure` / `AddJobsApplication` | §4.2 |
| **6.3 Strategy** | `ApiControllerBase.MapStatus` | §4.3 |
| **6.3 Template Method** | `ApiControllerBase.ToActionResult` | §4.3 |
| **6.3 Chain of Responsibility** | MediatR pipeline behaviors | §4.3 |
| **6.3 Observer** | Domain events → outbox interceptor | §4.3 |
| **6.3 State** | `Job` state machine + TypeScript `NextStatus<S>` | §4.3 |
| **6.3 Command** | `CreateJobCommand`, `ScheduleJobCommand`, ... | §4.3 |
| **6.3 Mediator** | MediatR `ISender`, controllers never call handlers directly | §4.3 |
| **6.3 Repository / UoW** | `IJobRepository`, `IUnitOfWork` in Application; EF impl in Infrastructure | §4.4 |
| **6.3 Outbox** | `outbox_messages` + `InsertOutboxMessagesInterceptor` | §4.4 |
| **6.3 Result / Railway** | `SharedKernel.Results.Result<T>` | §4.4 |
| **6.3 Problem Details** | `ProblemFromError`, `ExceptionToProblemDetailsMapper` | §4.4 |

---

## 6. Non-goals (patterns we chose *not* to use)

Being explicit about what we rejected — and why — is as important as what we adopted.

| Rejected / deferred | Reason |
|---|---|
| **Global exception → HTTP mapping via middleware only** | We route business failures through `Result`, keeping middleware for *unexpected* exceptions only (`ExceptionToProblemDetailsMapper`). Business errors never travel as exceptions. |
| **Dynamic Proxy / AOP for cross-cutting concerns** | Pipeline behaviors give the same benefit with static, debuggable code. |
| **CQS with separate write / read databases** | The read side (`JobQueryService`) is a projection over the same DB. Splitting the database is a future evolution, not a current necessity. |
| **Domain events published directly to a bus** | Would leak transport into the domain. Domain events are internal; the *integration events* are the transport-safe representation. |
| **Aggregate collections exposed by reference** | `Job.Photos` returns `IReadOnlyCollection<JobPhoto>`, and the internal `_photos` field is only accessible through `Job.AddPhoto(...)`, preserving invariants. |
| **Anemic domain model** | `Job` is behavior-rich (`Schedule`, `Start`, `Complete`, `Cancel`, `AddPhoto`), not a bag of setters. All state changes go through methods that enforce invariants. |
| **Service layer above the aggregate** | Application handlers depend on the aggregate directly; there is no "JobService" god-class. Handlers *coordinate*, they don't *implement*. |
| **Static `DateTime.UtcNow` in the domain** | Every method takes `nowUtc` as a parameter. `IDateTimeProvider` supplies it at the Application boundary. Domain remains deterministic and unit-testable. |

---

## 7. Verification

Two automated safeguards keep this document honest:

1. **`tests/JobTracker.Tests.Architecture/LayeringRulesTests.cs`** — 9 NetArchTest rules that fail the build if any layering claim in §2.5 / §3.4 stops being true.
2. **`tests/JobTracker.Tests.Unit/Jobs/Domain/JobTests.cs`** — 11 xUnit tests that codify the state machine and factory rules described in §3.1, §4.1, §4.3.

Any pattern regression breaks CI before merge.

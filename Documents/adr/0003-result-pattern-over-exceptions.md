# ADR-0003 — Result Pattern over Exceptions for Flow Control

- **Status:** Accepted
- **Date:** 2026-08-14
- **Deciders:** Team Lead

## Context

Application-layer handlers surface **expected failure paths**: invalid input, invariant violations, not-found aggregates, invalid state transitions, concurrency conflicts. Two idioms exist:

| Idiom | Pros | Cons |
|---|---|---|
| Exceptions for every failure (`throw new NotFoundException(...)`) | Idiomatic in older .NET code, terse call sites. | Non-local control flow, expensive throw+catch, exceptions become part of the API contract implicitly, easy to lose stack info, harder to test failure paths deterministically. |
| **`Result<T>` with an `Error` payload** | Explicit failure branch, easily composable, testable, no perf cost, forces the caller to handle the failure or explicitly `throw`. | A little more code at handlers, needs mapping to HTTP. |

## Decision

**Return `Result<T>` from every application handler and every domain factory / behavior method.**
- `Result<T>` is a `readonly record struct`; carries either a `T Value` or an `Error { Code, Message, Type }`.
- Exceptions are reserved for **truly exceptional / bug** situations (`DbUpdateException`, deserialization, unexpected null).
- Controllers translate `Result<T>` to HTTP: 200/201 on success, RFC 7807 ProblemDetails on failure keyed by `Error.Type` (`Validation`→400, `NotFound`→404, `Conflict`→409, `Unauthorized`→401/403, `Unexpected`→500).
- MediatR `ValidationBehavior` returns a typed `Result` failure when FluentValidation fails.

## Consequences

**Positive**
- Failure paths are as testable as happy paths.
- Error codes are stable and documented (see `04-api-contracts.md §8`).
- No hidden throws; the pipeline behavior does not need `catch` scaffolding for expected failures.
- Handlers stay short and focused.

**Negative**
- Slight verbosity at the handler edge (`if (r.IsFailure) return r.Error;` chains).
- Team members coming from Java/older .NET need to unlearn the exception habit.

## References

- SharedKernel `Result`, `Result<T>`, `Error` in 03-backend-solution.md §4
- 04-api-contracts.md §1 (ProblemDetails mapping)

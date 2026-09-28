---
paths:
  - "test/**"
---

# ToolShare tests (`test/`)

Rules for creating, changing, and running tests under `test/`. Binding baseline: Principle V of
the constitution (`.specify/memory/constitution.md`) — xUnit + ABP test base; domain rules are
covered by unit tests **without a database**, application-layer behavior by integration tests
against **real PostgreSQL via Testcontainers**. SQLite, EF Core InMemory, or any other database
substitute is forbidden. A feature is not done until its tests exist and pass.

## Stack

- .NET SDK `10.0.302` (`global.json`), `net10.0`, `Nullable` enabled, shared `common.props`.
- xUnit 2.9.3 + `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` 17.14.1.
- Shouldly (assertions), NSubstitute (mocks — only where no real implementation fits).
- `Volo.Abp.TestBase` / `Volo.Abp.Autofac` 10.6.0 (`AbpIntegratedTest<TModule>`).
- `Testcontainers.PostgreSql` 4.13.0, image `postgres:16-alpine`, Npgsql.
- No `.runsettings`, no `xunit.runner.json`, no `[Trait]` categories, no `Skip` — tests are
  distinguished only by project and fully qualified name. There is no CI pipeline in the repo;
  tests are run locally via `dotnet test`.

## Test project map

| Project | Kind | Database |
|---|---|---|
| `ToolShare.TestBase` | Shared infrastructure: `PostgreSqlContainerFixture`, `ToolShareTestBase<TModule>`, `ToolShareTestBaseModule`, `FakeCurrentPrincipalAccessor` | — |
| `ToolShare.<Module>.Domain.Tests` (Catalog, Membership, Lending, Notifications) | Pure unit tests of entities/domain services. References **only** its own `*.Domain` — no ABP host | No |
| `ToolShare.<Module>.Application.Tests` (Catalog, Membership, Lending, Notifications) | Integration tests of app services with real authorization, the enrolment gate, and cross-module contracts (`PublicContract/`) | Yes, Testcontainers |
| `ToolShare.Domain.Tests`, `ToolShare.Application.Tests` | Host-level sample tests from the ABP startup template; use `ToolShareTestBaseModule` with `AddAlwaysAllowAuthorization()` | Via the EF project |
| `ToolShare.EntityFrameworkCore.Tests` | Host EF/migration level: one migrated database per collection, read-only tests | Yes, Testcontainers |

Every test project is listed under the `/test/` folder in `ToolShare.slnx` — a new project must be
added there.

## How tests are run today

### Prerequisites

- **Docker must be running** for any `*.Application.Tests` and `EntityFrameworkCore.Tests`.
  `docker compose up` is **not** needed for tests — each test assembly starts its own PostgreSQL
  container. Domain tests don't need Docker.
- The repository path contains spaces — always quote paths in Bash.

### Commands (from the `tools-sharing/` root)

```bash
# Whole solution (builds everything; each Application/EF assembly starts its own container)
dotnet test ToolShare.slnx

# One project
dotnet test test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj

# One test class
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj --filter "FullyQualifiedName~CheckOutTests"

# One test
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj --filter "FullyQualifiedName~CheckOutTests.Checking_out_against_an_active_reservation_marks_the_instance_on_loan"

# A whole feature slice (namespace), e.g. all reservation tests
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj --filter "FullyQualifiedName~ToolShare.Lending.Reservations"

# Fast loop without rebuilding (after dotnet build)
dotnet test test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj --no-build
```

Rough timings: a domain project ~5–10 s; one application test class ~30–40 s (mostly container
start + template migration). While working on a feature, run the narrowest filter. Inside
`/speckit-implement` the final full run belongs to the gate — see `.claude/rules/speckit-gate.md`.
Outside Spec Kit, run the whole affected project before finishing, and for cross-module changes
`dotnet test ToolShare.slnx`.

Not a test, but part of verifying module boundaries (Principle II):

```bash
pwsh scripts/check-module-boundaries.ps1
```

It scans `src/` `ProjectReference`s and fails if a project references another module's
`*.Domain`/`*.EntityFrameworkCore`. Allowed exceptions: `ToolShare.Blazor` / `ToolShare.DbMigrator`
(composition roots) and `ToolShare.EntityFrameworkCore` → module `*.EntityFrameworkCore` only (the
consolidated `ToolShareDbContext`). It must be green alongside the tests.

Every test gets its own database cloned from a migrated template, tests within one assembly run
sequentially, the enrolment gate is enforced for real, and `ToolShare.EntityFrameworkCore.Tests`
shares one database (read-only tests). Fixture internals, common fixture failures, the fix loop
and the Spec Kit gate: `test/README.md`. Evals of the agentic tooling: `evals/README.md`.

## Writing new tests

### Where they go

- Domain rule (entity invariant, state transition, calculation) → `ToolShare.<Module>.Domain.Tests`.
- App-service behavior, authorization, persistence, concurrency, ETOs/events, reports →
  `ToolShare.<Module>.Application.Tests`.
- Folder layout mirrors the module's feature slices: `Loans/`, `Reservations/`, `Maintenance/`,
  `Reports/`, `Members/`, `ToolInstances/`, etc.; plus the cross-cutting `Authorization/`,
  `Permissions/`, `PublicContract/`, `Concurrency/`, `Seeding/`.
- Namespace = the slice namespace in production code (`ToolShare.Lending.Loans`), not the test
  project name.

### Naming and style

- One file = one behavior/scenario: `CheckOutTests`, `EarlyReturnTests`, `OverlapAndWaitlistTests`.
- Method names are underscore-separated sentences describing the expected behavior:
  `Checking_out_without_a_matching_active_reservation_is_rejected`.
- A class-level `/// <summary>` citing requirement IDs from `spec.md` (`FR-010, LOAN-01`) and a short
  note on what's verified.
- Assertions via Shouldly only (`ShouldBe`, `ShouldBeNull`, `ShouldBeEmpty`, `Should.Throw[Async]<T>`).
- For business errors assert the code, not just the type:
  `exception.Code.ShouldBe(LendingDomainErrorCodes.InstanceUnavailable)`.
- `[Theory]` + `[InlineData]` for state-transition/combination tables.

### Domain tests

- A plain class with no base class and no DI; entities are built via `new` / domain factories.
- Time is a fixed constant, e.g. `private static readonly DateTime Now = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);`
  — never `DateTime.Now`.
- No references to `EntityFrameworkCore`, `Application`, or other modules (the csproj references
  only its own `*.Domain`).

### Application tests

- Inherit `<Module>AuthorizationTestBase` (impersonation and seeding helpers), or
  `<Module>ApplicationTestBase` when no impersonation is needed. By default a test runs as the
  "test-runner" principal with role `admin` and an Active Administrator `Member`.
- Resolve services in the constructor via `GetRequiredService<T>()`, using **interfaces from
  `*.Application.Contracts`** (`ILoanAppService`, `IToolInstanceLookupAppService`).
- Switch users only through a `using` block called directly in the test (never return the
  `IDisposable` across an `await`):
  ```csharp
  using (AsLibrarian()) { loan = await _loanAppService.CheckOutAsync(...); }
  ```
  Available: `AsMemberWithNoGrants()`, `AsLibrarian()`, `AsAnonymous()`,
  `AsAuthenticatedNonMember()`, `Impersonate(await BuildAdminPrincipalAsync())`.
- Create test data **through app services**, the way a user would (e.g. `SeedCatalogDataAsync()`
  creates a category/tool/instance as admin), not by inserting directly into another module's
  repositories. Unique values (names, serial numbers) come from `Guid.NewGuid()`.
- Direct access to the module's own repository (via `WithUnitOfWorkAsync`) is acceptable only to
  verify persistence/append-only history that isn't visible through DTOs.
- Every new authorization requirement → tests for both allow and deny (`AbpAuthorizationException`),
  including the non-member case via `AsAuthenticatedNonMember()`.
- Need a new kind of user → add a fixed `Guid` to `<Module>TestPrincipals` and its
  `EnsureMemberAsync(...)` call to `<Module>TestDataSeedContributor` (seeding is idempotent and runs
  once into the template database), plus an `As...()` helper in `<Module>AuthorizationTestBase`.
- Replace external dependencies with test implementations via DI, like `FakeEmailSender` in
  Notifications (records mail in memory; `ThrowOnSend` simulates failure) — never real network calls.

### Cross-module contract tests (`PublicContract/`)

- Files in `PublicContract/` **must not** have a `using` for the producing module's
  `*.EntityFrameworkCore`, `*.Repositories`, `*.Migrations`, `*.Data` namespaces, nor mention its
  Domain types by name (`Tool`, `ToolInstance`, `CatalogDbContext`, `IToolRepository`, ...).
  `ContractIsolationTests` scans this folder's source and fails on a violation — the absent import
  is the assertion.
- Tier 1 contracts (`IToolInstanceLookupAppService`, `*Eto`, public enums) are tested the way a
  consumer sees them: resolve the app service from contracts + receive the ETO via `ILocalEventBus`.

### Performance and concurrency

- Performance tests (`*PerformanceTests`) measure warm p95 with `Stopwatch` after a warm-up and
  assert the threshold from `plan.md`. Don't add caching or mocks just to make them pass.
- Concurrency is verified with parallel real calls (`Task.WhenAll`) against PostgreSQL (e.g. the
  `EXCLUDE USING gist` constraint on reservations, `ConcurrencyStamp` for optimistic concurrency).

## New test projects for a new module

Copy the structure of `ToolShare.Lending.Application.Tests`; the checklist of csproj references
and infrastructure files is in `test/README.md`. Add both projects to `/test/` in
`ToolShare.slnx`.

## Forbidden

- SQLite, `UseInMemoryDatabase`, or mocked repositories/`DbContext` instead of a real database in
  application tests.
- A custom `new PostgreSqlBuilder(...)` in tests — always go through `PostgreSqlContainerFixture`.
- Mutating data in the template database, or in the shared `EntityFrameworkCore.Tests` database,
  from a test method.
- `AddAlwaysAllowAuthorization()` in module test modules (it stays only in the host template's
  `ToolShareTestBaseModule`).
- `Guid.NewGuid()` as the id of a principal that actually calls an app service (except the
  deliberate "not enrolled" scenario).
- `DateTime.Now` / dependence on the current date in domain tests; `Thread.Sleep` for synchronization.
- Marking a test `Skip` or deleting it to get a green run — fix the cause or report it.

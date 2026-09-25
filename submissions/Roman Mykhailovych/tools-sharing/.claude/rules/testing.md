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
start + template migration). While working on a feature, run the narrowest filter; before
finishing, run the whole affected project, and for cross-module changes `dotnet test ToolShare.slnx`.

Not a test, but part of verifying module boundaries (Principle II):

```bash
pwsh scripts/check-module-boundaries.ps1
```

It scans `src/` `ProjectReference`s and fails if a project references another module's
`*.Domain`/`*.EntityFrameworkCore`. Allowed exceptions: `ToolShare.Blazor` / `ToolShare.DbMigrator`
(composition roots) and `ToolShare.EntityFrameworkCore` → module `*.EntityFrameworkCore` only (the
consolidated `ToolShareDbContext`). It must be green alongside the tests.

### Automated fix loop

`scripts/agent-loop.ps1` runs `dotnet test <project>` plus the boundary audit and, while either is
red, hands the failures to a headless `claude -p` agent that fixes one failure per iteration in
`src/` — until green, `-MaxIterations`, no progress, or a violation (the agent touched `test/` or
the audit script). Logs go to `loop-runs/<timestamp>/run.md`.

```bash
pwsh scripts/agent-loop.ps1 -Project test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj -MaxIterations 5
```

### What happens under the hood of an integration run

1. xUnit creates the module's **collection fixture** (`<Module>ApplicationTestFixture`, wired via
   `[CollectionDefinition]` in `<Module>ApplicationTestCollection.cs`). Every test class in the
   assembly carries the same `[Collection(...)]`, so **tests within one assembly run sequentially**,
   while `dotnet test` runs different assemblies in parallel — each with its own container.
2. The fixture starts `PostgreSqlContainerFixture` (`postgres:16-alpine`, `max_connections=300`).
3. `EnsureTemplateMigratedAsync` migrates the template database `toolshare_template` **once per
   assembly**: `ToolShareDbContext.Database.MigrateAsync()` (consolidated migrations for all
   schemas), then a throwaway ABP application `<Module>AuthorizationSeedModule` runs the same
   `IDataSeeder` pipeline as `ToolShare.DbMigrator` (admin, roles, permissions, a Member for admin),
   plus `<Module>TestDataSeedContributor` (Member rows for the fixed test principals).
   Then `NpgsqlConnection.ClearAllPools()`, because `CREATE DATABASE ... TEMPLATE` fails while any
   connection to the template is still open.
4. For **every test method** (xUnit creates a new class instance → `AbpIntegratedTest` boots a new
   ABP application), `<Module>ApplicationTestModule.ConfigureServices` calls `CreateDatabaseAsync()`
   — a new `test_<guid>` database is cloned from the template and every module `DbContext`
   (`AbpDbContextOptions`) is pointed at it. So **each test gets its own clean database**: no
   cleanup is needed and test order doesn't matter.
5. Module test modules **deliberately do not** depend on `ToolShareTestBaseModule` and do not call
   `AddAlwaysAllowAuthorization()` — permissions and `MembershipMethodInvocationAuthorizationService`
   (the enrolment gate) are enforced for real.

`ToolShare.EntityFrameworkCore.Tests` is the exception: one migrated database for the whole
collection with no cloning, so tests there must be read-only.

### Common failures

- `DockerUnavailableException` / failure in the fixture's `InitializeAsync` → Docker Desktop isn't running.
- `source database "toolshare_template" is being accessed by other users` → something still holds a
  connection to the template after migration; never open connections to `TemplateConnectionString`
  outside `EnsureTemplateMigratedAsync`.
- `too many clients already` → a test isn't releasing connections/`DbContext`s; the limit is already 300.
- The enrolment gate rejects a call (`AbpAuthorizationException`) → the principal has no Active
  `Member` row: use a fixed id from `<Module>TestPrincipals`, not `Guid.NewGuid()`.

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

Copy the structure of an existing one (the most complete example is
`ToolShare.Lending.Application.Tests`):

- **Domain.Tests csproj**: references only `src/ToolShare.<Module>.Domain`; packages xunit,
  Shouldly, NSubstitute, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`; `IsPackable=false`;
  `RootNamespace=ToolShare.<Module>`; `<Import Project="..\..\common.props" />`.
- **Application.Tests csproj**: its own `Application` + `EntityFrameworkCore`, the
  Application/EntityFrameworkCore projects of modules it composes with (Membership is mandatory
  because of the enrolment gate), host `ToolShare.Application` + `ToolShare.EntityFrameworkCore`,
  `ToolShare.TestBase`; packages `Volo.Abp.TestBase`, `Volo.Abp.Autofac`, `Volo.Abp.Authorization`,
  `Volo.Abp.EventBus` (10.6.0).
- Infrastructure files: `<Module>ApplicationTestFixture` (container + template migration/seeding),
  `<Module>ApplicationTestCollection` (`ICollectionFixture` + a const with the collection name),
  `<Module>ApplicationTestModule` (`[DependsOn]` modules, `UseNpgsql` on the cloned database for
  every `DbContext` with its own `MigrationsHistoryTable` in the module schema; **no**
  `AddAlwaysAllowAuthorization`), `<Module>AuthorizationSeedModule`, `<Module>ApplicationTestBase`,
  `<Module>AuthorizationTestBase`, `<Module>TestPrincipals` (unique fixed `Guid`s),
  `<Module>TestDataSeedContributor`.
- Add both projects to `/test/` in `ToolShare.slnx` and verify with `dotnet test ToolShare.slnx`.

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

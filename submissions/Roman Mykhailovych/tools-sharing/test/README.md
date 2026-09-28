# ToolShare test infrastructure

Reference material for the test setup. The rules for writing and running tests are in
`.claude/rules/testing.md` (loaded automatically when working under `test/`); this file holds the
internals you only need when changing the infrastructure itself, adding a module, or debugging a
fixture failure.

## What happens under the hood of an integration run

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
   cleanup is needed and test order doesn't matter. Each cloned database's Npgsql pool is capped
   at **10** connections, and when the next database is cloned, the **previous** one's pool is
   cleared. Tests run sequentially, so that test's application is already disposed by then.
   Before this (008-out-of-band-maintenance), every per-test database kept its idle pooled
   connections open for the whole run. Once `ToolShare.Lending.Application.Tests` grew to 139 tests,
   including parallel concurrency tests, a full run exceeded `max_connections=300`
   (`53300: sorry, too many clients already`) while every filtered run still passed.
5. Module test modules **deliberately do not** depend on `ToolShareTestBaseModule` and do not call
   `AddAlwaysAllowAuthorization()` — permissions and `MembershipMethodInvocationAuthorizationService`
   (the enrolment gate) are enforced for real.

`ToolShare.EntityFrameworkCore.Tests` is the exception: one migrated database for the whole
collection with no cloning, so tests there must be read-only.

## Common failures

- `DockerUnavailableException` / failure in the fixture's `InitializeAsync` → Docker Desktop isn't running.
- `source database "toolshare_template" is being accessed by other users` → something still holds a
  connection to the template after migration; never open connections to `TemplateConnectionString`
  outside `EnsureTemplateMigratedAsync`.
- `too many clients already` → a test isn't releasing connections/`DbContext`s. The limit is already
  300, and each test's pool is capped at 10 and released when the next test starts (step 4). So a
  single test holding more than 10 connections at once, or a test class that bypasses
  `CreateDatabaseAsync`, is the likely cause. Don't raise the limit or the cap to hide it.
- The enrolment gate rejects a call (`AbpAuthorizationException`) → the principal has no Active
  `Member` row: use a fixed id from `<Module>TestPrincipals`, not `Guid.NewGuid()`.

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

## Automated fix loop and the Spec Kit gate

`scripts/fix-until-green.ps1` runs `dotnet test <project>` plus the boundary audit and, while either
is red, hands all listed failures to the headless `toolshare-fixer` agent
(`.claude/agents/toolshare-fixer.md`), which may change `src/` only — until green,
`-MaxIterations`, no progress (same failures twice), or a violation (the agent touched `test/` or
the audit script). Logs: `green-runs/<timestamp>/run.log`.

```bash
pwsh scripts/fix-until-green.ps1 -Project test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj -MaxIterations 5
```

`scripts/speckit-gate.ps1` is the mandatory `after_implement` hook in `.specify/extensions.yml`
(`speckit.green.gate` → `/speckit-green-gate`). It builds `ToolShare.slnx` once, runs the boundary
audit once, runs every test project referenced in the feature's `tasks.md` in parallel, sends only
the red projects to the fix loop, and re-checks once after any fixing. Already green = no agent
call, $0. `-NoFix` = check only; `-ReuseVerdict` = return the verdict saved in
`green-runs/last-gate.json` when the working tree hasn't changed since. Exit 5 = Docker not running
(no agent is invoked for environment failures). The E2E suite is never part of the gate.

## Browser E2E suite (`ToolShare.E2E.Tests`)

Playwright (Chromium) against the whole app: `E2EAppFixture` starts a PostgreSQL container, runs
`ToolShare.DbMigrator` (migrations + the seeded `admin`), starts `ToolShare.Blazor` with
`dotnet run` on a free port (Development, http), and shares one headless browser; each test gets
its own browser context. Blazor Server buttons are prerendered before the circuit is interactive,
so clicks that must trigger UI go through `ClickUntilVisibleAsync`. Wrap every test body in
`RunAsync(...)`: on failure it saves `<Class>.<Test>.trace.zip` + `.png` and appends the app's
recent output to the failure message.

It runs **once, check-only, at the very end** — `scripts/speckit-e2e.ps1`, the `after_converge`
hook (`speckit.e2e.check` → `/speckit-e2e-check`): skipped when `tasks.md` touches no
`src/ToolShare.*Blazor/` code, refused while tasks are open or the gate isn't PASS. No fix loop.

```bash
pwsh scripts/speckit-e2e.ps1 -Force                              # full final check
dotnet test test/ToolShare.E2E.Tests/ToolShare.E2E.Tests.csproj   # the suite alone
pwsh test/ToolShare.E2E.Tests/bin/Debug/net10.0/playwright.ps1 show-trace <trace.zip>
```

Needs Docker; the first run downloads Playwright's Chromium build (~250–300 MB). A run takes a few
minutes (app build + migration + startup). `TOOLSHARE_E2E_HEADED=1` shows the browser.

Evals of the fixer and the reviewer: see `evals/README.md`.

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ToolShare — an ABP Framework 10.5.x / .NET 10 layered monolith, Blazor Web App (InteractiveServer),
PostgreSQL 16 via EF Core. Built as a **modular monolith**: a host module (`ToolShare.*`) plus one
module per feature area — `ToolShare.Catalog.*` (tools, categories, tool instances),
`ToolShare.Membership.*` (roster, community rules, reliability rating; own PostgreSQL schema
`membership`), and `ToolShare.Lending.*` (reservations/waitlist, checkout/return, maintenance
requests; own PostgreSQL schema `lending`). Lending's `Reservations` table uses a PostgreSQL
`EXCLUDE USING gist` constraint (the `btree_gist` extension, enabled in its EF Core migration) to
make double-booking an instance impossible under concurrency — there is no fluent-API equivalent,
so it's added via raw SQL in the migration. The project is driven by Spec Kit (`.specify/`,
`specs/*/spec.md|plan.md|tasks.md`) — features are specified, clarified, planned, and task-broken
before implementation.

**`.specify/memory/constitution.md` is binding.** Read it before making architectural decisions;
every plan and PR is checked against it. Key points repeated here because they shape almost every
change:

- **Fixed stack, non-negotiable**: .NET 10, ABP 10.5.x free/OSS only (no ABP Commercial), Blazor
  InteractiveServer, PostgreSQL 16/EF Core. No alternative ORM, DB, or UI framework.
- **Strict module boundaries**: a module must never reference another module's `Domain` or
  `EntityFrameworkCore` project. The only allowed cross-module coupling is (a) types in the other
  module's `*.Application.Contracts`, and (b) integration events over `ILocalEventBus`.
- **Schema-level data isolation**: each module has its own `DbContext` and PostgreSQL schema; no
  cross-schema foreign keys. Reference another module's data by ID only.
- **Append-only history**: state-changing domain facts that matter for audit (loans, returns,
  condition/circulation changes) are recorded as new rows, never mutated or deleted in place.
- **Membership-gated, not merely authentication-gated**: since `ToolShare.Membership.*` shipped,
  every application-service call — including Catalog's browsing methods — is refused unless the
  caller resolves to an enrolled, **Active** `Member`, enforced by a decorator over ABP's
  `IMethodInvocationAuthorizationService` (`MembershipMethodInvocationAuthorizationService`). This
  supersedes 002's original "any authenticated user may browse" rule.
- **Test-first, non-negotiable**: domain rules get unit tests with no database; application-layer
  behavior gets integration tests against a **real PostgreSQL via Testcontainers** — never SQLite
  or in-memory. A feature isn't done until its tests exist and pass.
- **Container-first**: must build/test via plain `dotnet` CLI (no IDE dependency); runs via
  `docker compose` (app + postgres).

## Commands

```bash
# Restore/build the whole solution
dotnet build ToolShare.slnx

# Run all tests
dotnet test ToolShare.slnx

# Run one test project
dotnet test test/ToolShare.Catalog.Application.Tests/ToolShare.Catalog.Application.Tests.csproj

# Run a single test (xUnit fully-qualified name filter)
dotnet test test/ToolShare.Catalog.Application.Tests/ToolShare.Catalog.Application.Tests.csproj --filter "FullyQualifiedName~ToolAppServiceTests.Should_Create_Tool"

# Start PostgreSQL for local dev (matches connection strings in appsettings.json)
docker compose up -d
docker compose down        # add -v to also drop the volume

# Create/migrate the database (first run, and after any new migration) — must run from its own
# directory, not via --project from repo root, or appsettings.json won't resolve
cd src/ToolShare.DbMigrator && dotnet run

# Run the Blazor host app
cd src/ToolShare.Blazor && dotnet run

# Install client-side npm libs for MVC/Blazor UI (rarely needed manually)
abp install-libs
```

Application-layer and EF Core integration tests spin up their own PostgreSQL container via
Testcontainers (see `test/ToolShare.TestBase/PostgreSqlContainerFixture.cs`) — Docker must be
running, but you don't need `docker compose up` just to run tests.

## Architecture

### Module anatomy

Every module (host `ToolShare` and each feature module — `ToolShare.Catalog`, `ToolShare.Membership`,
`ToolShare.Lending`) is split into the same ABP layer set, and dependencies only flow downward
through this list:

- `*.Domain.Shared` — enums, error codes, cross-cutting constants, and (important) any
  **integration event (ETO)** the module publishes. ETOs live here rather than in
  `Application.Contracts` because `Domain` raises them via `AddLocalEvent` and `Domain` cannot
  reference `Application.Contracts`.
- `*.Domain` — entities, aggregate roots, domain services, repository *interfaces*. Never
  referenced from outside the module.
- `*.EntityFrameworkCore` — the module's own `DbContext` (own PostgreSQL schema), EF
  configurations, repository *implementations*. Never referenced from outside the module.
- `*.Application.Contracts` — app-service interfaces, DTOs, permission definitions. This is the
  module's **published boundary**; other modules and the module's own Blazor UI depend on this,
  never on `Domain` or `EntityFrameworkCore` directly.
- `*.Application` — app-service implementations, mappers.
- `*.Blazor` — the module's Razor components/pages, consuming only its own
  `Application.Contracts` (and, for cross-module UI composition, another module's contracts).

`ToolShare.HttpApi` and `ToolShare.DbMigrator` sit at the host level; `DbMigrator` is the only
place migrations are applied — never implicitly at request time in production.

### Stability tiers on a module's public surface (see `specs/002-catalog-foundation/contracts/README.md`)

- **Tier 1 — Public, frozen once shipped**: the interfaces/DTOs/ETOs/enums other modules actually
  depend on (e.g. `IToolInstanceLookupAppService`, `ToolInstanceLookupDto`,
  `ToolInstanceStateChangedEto`, `ToolCondition`, `ToolInstanceCirculationState`). Changes must be
  additive only (new optional members, new enum values with new numbers) — renaming, removing, or
  renumbering is breaking and requires coordinating every consumer. `ToolInstanceCirculationState`
  gained `OnLoan`/`UnderMaintenance` (004-lending) via Catalog's inbound
  `IToolInstanceCirculationReportingAppService` contract — an additive change, so every existing
  consumer keeps working unchanged.
- **Tier 2 — Module-internal**: the rest of `Application.Contracts`, consumed only by that
  module's own Blazor project. C# accessibility may permit another module to reference it, but
  doing so is a Principle II violation regardless.

Cross-module contract tests prove the boundary by asserting a downstream-style test can resolve
the public app service and receive the ETO **without importing the producing module's `Domain` or
`EntityFrameworkCore` namespaces** — the absent import is the assertion.

### Feature slices within a module

Inside `Domain`, `Application.Contracts`, and `Application`, code is organized by feature slice
(e.g. `Categories/`, `Tools/`, `ToolInstances/` under `ToolShare.Catalog.*`; `Reservations/`,
`Loans/`, `Maintenance/`, `Reports/` under `ToolShare.Lending.*`), each with its entity/aggregate
root, repository interface, app service, DTOs, and mapper — not by technical layer within the slice.
A slice need not appear in every layer: Lending's `Reports/` (006-librarian-reports — the three
librarian reports, all read-only projections over history Lending already owns) exists only in
`Application.Contracts`, `Application`, and `Blazor`, because it adds no entity and no repository.

### Testing layout

- `test/ToolShare.TestBase` — shared `PostgreSqlContainerFixture`: one Testcontainers PostgreSQL
  instance per test assembly, migrated once into a template database, then cloned per test class
  via `CREATE DATABASE ... TEMPLATE ...` for speed. Shared by the host and every module's test
  projects so this setup exists exactly once.
- `test/ToolShare.<Module>.Domain.Tests` — pure unit tests, no database.
- `test/ToolShare.<Module>.Application.Tests` — integration tests against the real Testcontainers
  Postgres, including the cross-module contract tests described above.
- `test/ToolShare.EntityFrameworkCore.Tests` — EF/migration-level tests.

### Spec Kit workflow

Features are developed one at a time via `.specify/` + `specs/<NNN-feature-name>/`: `spec.md` →
`plan.md` → `tasks.md`, using the `speckit-*` skills (`/speckit-specify`, `/speckit-clarify`,
`/speckit-plan`, `/speckit-tasks`, `/speckit-implement`, `/speckit-analyze`). A module's
`Application.Contracts` are specified before or alongside the consumers that depend on them.
`spec.md` holds a feature's functional requirements; the constitution holds only cross-cutting
principles — don't conflate the two when writing or editing either.
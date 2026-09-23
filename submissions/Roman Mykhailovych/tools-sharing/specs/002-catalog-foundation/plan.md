# Implementation Plan: Application Foundation & Catalog Vertical Slice

**Branch**: `002-catalog-foundation` | **Date**: 2026-07-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-catalog-foundation/spec.md`

## Summary

Stand up the ToolShare modular monolith and deliver the Catalog module as the first complete vertical slice.

The foundation is an ABP 10.5.0 layered solution (.NET 10, Blazor Web App in InteractiveServer mode, PostgreSQL 16 via EF Core 10) generated from the open-source `app` template, hardened into a modular monolith: the host owns identity/authorization and composition, while **Catalog** ships as six projects (`Domain.Shared`, `Domain`, `Application.Contracts`, `Application`, `EntityFrameworkCore`, `Blazor`) with its **own `CatalogDbContext` mapped to the `catalog` PostgreSQL schema** inside the shared database. A dedicated `ToolShare.DbMigrator` composes both schema migrators and idempotent data seeders (admin, `Librarian` role, starter categories); a multi-stage `Dockerfile` plus `docker compose` (`app`, `postgres`) makes the whole thing reproducible from a clean checkout.

The Catalog slice covers `Category` → `Tool` → `ToolInstance` with catalog-wide unique serial numbers, the fixed 4-level condition scale, an append-only `ToolInstanceStateChange` history, photos via ABP BlobStoring, CRUD + paged accent-tolerant search behind a permission boundary, and a Blazor UI. Its published boundary — `IToolInstanceLookupAppService` + `ToolInstanceStateChangedEto` on `ILocalEventBus` — is the extension point Lending/Maintenance (003+) will consume without touching Catalog internals. Tests are xUnit: pure domain rules without a database, application behavior against **real PostgreSQL via Testcontainers** (replacing the template's in-memory SQLite, which Principle V forbids).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`), pinned with `global.json` → SDK `10.0.301` `rollForward: latestFeature` (verified installed; runtime 10.0.9)

**Primary Dependencies**: ABP Framework 10.5.0 (free/open-source only — `Volo.Abp.Identity`, `Volo.Abp.PermissionManagement`, `Volo.Abp.OpenIddict`, `Volo.Abp.BlobStoring.FileSystem`, `Volo.Abp.EntityFrameworkCore.PostgreSql`), Blazor Web App (InteractiveServer) with Blazorise + LeptonX Lite theme, EF Core 10.0.7 / Npgsql 10 (transitive via ABP)

**Storage**: PostgreSQL 16 (`postgres:16-alpine`), single database `toolshare`; host schema `public`, Catalog schema `catalog`; photo binaries in ABP BlobStoring FileSystem container on a mounted volume (not in the database)

**Testing**: xUnit 2.9.3 + Shouldly 4.3 + NSubstitute 5.3, Microsoft.NET.Test.Sdk 17.14.1 (ABP 10.5 still ships xUnit v2 — no xUnit v3 migration is in scope); ABP test base for DI-backed tests; `Testcontainers.PostgreSql` 4.13.0 for all integration tests (no SQLite/in-memory provider anywhere)

**Build tooling**: `dotnet-ef` **10.x** is required — the machine currently has the 9.0.9 global tool, which fails against the .NET 10 runtime (see research §1a)

**Target Platform**: Linux containers via `docker compose` (app + postgres); developer machines Windows/macOS/Linux through the `dotnet` CLI

**Project Type**: Modular-monolith web application — single ASP.NET Core host serving a server-rendered Blazor UI, composed of ABP modules

**Performance Goals**: Catalog search/list p95 < 500 ms at ~2 000 tools / ~5 000 instances with page size ≤ 50; tool detail with instances and photo metadata p95 < 400 ms; container cold start to first served page < 60 s including migration

**Constraints**: No cross-schema foreign keys; no cross-module references outside `*.Application.Contracts` + `ILocalEventBus`; history rows never updated or deleted; the running application never mutates its own schema; ABP Commercial packages forbidden; must build and test through `dotnet` CLI with no IDE dependency

**Scale/Scope**: Single community, single tenant (multi-tenancy disabled), tens of concurrent users, low hundreds of tools; this feature delivers ~6 Catalog projects + 3 test projects, 4 aggregates/entities, 4 application services, ~5 Blazor pages

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [.specify/memory/constitution.md](../../.specify/memory/constitution.md) v2.0.0.

> ✅ **GATE CLEARED — Principle I amended to .NET 10 / ABP 10.5.x in constitution v2.0.0 (2026-07-28).**
> Constitution v1.0.0 originally fixed the stack at .NET 9; the amendment to v2.0.0 retargets Principle I to .NET 10 / ABP 10.5.x, matching this plan. See the constitution's Sync Impact Report for the full rationale.

| # | Principle | Verdict | How this plan satisfies it |
|---|-----------|---------|----------------------------|
| I | Fixed Stack (NON-NEGOTIABLE) | **PASS** | The plan targets **.NET 10 (`net10.0`) and ABP 10.5.0**, matching constitution v2.0.0. All packages come from the free/open-source feed (no ABP Commercial), Blazor Web App is forced to InteractiveServer, and storage stays PostgreSQL 16 via EF Core/Npgsql. `global.json` pins SDK 10.0.301 so the target framework cannot drift. **[verified]** a probe generation with `-v 10.5.0` produced `net10.0` projects that build. |
| II | Modular Monolith With Strict Boundaries | **PASS** | Catalog ships as exactly the six prescribed projects. The host (`ToolShare.Blazor`, `ToolShare.DbMigrator`) is the composition root and may reference module projects; **no module references another module's `Domain` or `EntityFrameworkCore`**. Downstream modules will consume `ToolShare.Catalog.Application.Contracts` (interfaces + DTOs) and `ILocalEventBus` events only. See the boundary note below. |
| III | Schema-Level Data Isolation; No Cross-Module FKs | **PASS** | `CatalogDbContext` owns schema `catalog` with its own `catalog.__EFMigrationsHistory`. FKs exist only *within* the `catalog` schema (Tool→Category, ToolInstance→Tool, Photo/StateChange→ToolInstance). References to identity (`CreatorId`, `ChangedByUserId`) are plain `uuid` columns with **no FK** to `public."AbpUsers"`. |
| IV | Append-Only History | **PASS** | `ToolInstanceStateChange` is insert-only: no update/delete path in the domain, no setter surface after construction, no `Update`/`Delete` repository method exposed, and every condition/circulation transition — including initial registration and retirement — appends exactly one row. Retirement is a state change, never a hard delete. |
| V | Test-First Discipline (NON-NEGOTIABLE) | **PASS (with required template change)** | Domain rules (serial-number validity/normalization, condition and circulation transitions, availability) are unit-tested with no database. Application behavior is integration-tested against **real PostgreSQL via Testcontainers**. The generated template ships `Volo.Abp.EntityFrameworkCore.Sqlite` in-memory test wiring — this plan **removes** it, because Principle V names that substitute explicitly. |
| VI | IDE-Agnostic, Container-First | **PASS** | Solution is generated and built entirely with the `dotnet`/`abp` CLI (verified end-to-end during Phase 0); `dotnet build` + `dotnet test` are the only required entry points. Multi-stage `Dockerfile` (`sdk:10.0` → `aspnet:10.0`) + `docker compose` (`app`, `postgres`). Migrations are applied **only** by the dedicated `ToolShare.DbMigrator` project — never at request time. One new prerequisite: the `dotnet-ef` global tool must be upgraded to 10.x, since 9.0.9 cannot scaffold against the .NET 10 runtime. |

### Constitution amendment history (Principle I)

Ratified as constitution **v2.0.0** on 2026-07-28 — see the Sync Impact Report at the top of [.specify/memory/constitution.md](../../.specify/memory/constitution.md) for the full rationale (ABP 10.x is the actively developed line; .NET 9 was the trailing release; adopting the new target before any production code exists is far cheaper than migrating a built system later). Scope of the change was confined to target framework, package versions and container base images — no architectural principle (II–VI) was affected.

**Technology & Architecture Constraints check**: authentication/authorization use ABP's built-in `IdentityModule` + permission system (no hand-rolled auth) — **PASS**. No background work is in scope for this feature — **N/A**. Pure domain algorithms (serial-number normalization, name normalization for accent-tolerant search, transition rules, availability) live in `ToolShare.Catalog.Domain` free of EF Core/ABP infrastructure — **PASS**. No aggregate read models are rebuilt in this feature — **N/A**.

**Development Workflow check**: `ToolShare.Catalog.Application.Contracts` is defined in the same feature as, and ahead of, the consumers that will depend on it (003+) — **PASS**. This plan adds no functional requirements; those stay in [spec.md](./spec.md) — **PASS**.

### Boundary notes (recorded, not violations)

1. **`Domain.Shared` reaches downstream transitively.** `Catalog.Application.Contracts` references `Catalog.Domain.Shared` (ABP's standard home for enums, constants and localization). A downstream module referencing the contracts therefore also sees `Catalog.Domain.Shared`. This is not the `Domain` layer and carries no behavior — it is the intended ABP mechanism for sharing `ToolCondition`/`ToolInstanceCirculationState` across the boundary. Principle II's prohibition targets `Domain` and `EntityFrameworkCore`, which stay private.
2. **Migrations run at container start.** `docker compose` defines the two required services (`app`, `postgres`); the `app` image contains the published DbMigrator and its entrypoint runs `dotnet ToolShare.DbMigrator.dll` before `exec`ing the host, gated by `RUN_MIGRATIONS` (default `true`). Schema changes are still produced solely by the dedicated DbMigrator project, never by the running application at request time — Principle VI is satisfied in substance. Operators can also run the step alone: `docker compose run --rm --entrypoint "dotnet ToolShare.DbMigrator.dll" app`.

**Post-Phase 1 re-evaluation**: re-run after `data-model.md` and `contracts/` were written, and again after the .NET 10 / ABP 10.5 retarget and its constitution amendment (v2.0.0) — all six verdicts PASS. The designed aggregates introduce no cross-schema FK, the append-only history has no mutation path, and the published contract surface exposes only interfaces, DTOs and `Domain.Shared` enums. The retarget touched only framework/package versions and base images: **no change to [data-model.md](./data-model.md) or [contracts/](./contracts/) was needed**, which is itself evidence that the module boundary is insulated from the platform version.

## Project Structure

### Documentation (this feature)

```text
specs/002-catalog-foundation/
├── plan.md                             # This file (/speckit-plan output)
├── research.md                         # Phase 0 output — decisions & rationale
├── data-model.md                       # Phase 1 output — entities, rules, transitions
├── quickstart.md                       # Phase 1 output — run & validate the slice
├── contracts/                          # Phase 1 output
│   ├── README.md                       # Index + compatibility rules
│   ├── catalog-public-contracts.md     # FR-017 — the downstream-facing surface
│   ├── catalog-events.md               # FR-018 — ToolInstanceStateChangedEto
│   ├── catalog-app-services.md         # Module-internal service surface (UI contract)
│   └── catalog-permissions.md          # FR-012 — permission boundary
├── checklists/
│   └── requirements.md                 # Pre-existing spec quality checklist
└── tasks.md                            # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
global.json                             # Pins SDK 10.0.301, rollForward: latestFeature
ToolShare.sln
common.props                            # Shared LangVersion/ABP version (from template)
NuGet.Config
Dockerfile                              # Multi-stage: sdk:10.0 build → aspnet:10.0 runtime
docker-entrypoint.sh                    # Runs DbMigrator (if RUN_MIGRATIONS) then the host
docker-compose.yml                      # Services: app, postgres
docker-compose.override.yml             # Local dev overrides (ports, volumes)
.dockerignore

src/
├── ToolShare.Domain.Shared/            # Host: consts, shared enums, localization
├── ToolShare.Domain/                   # Host: identity/seed abstractions, IToolShareDbSchemaMigrator
├── ToolShare.Application.Contracts/    # Host: app-level DTOs/permissions
├── ToolShare.Application/              # Host: LibrarianRoleDataSeedContributor
├── ToolShare.EntityFrameworkCore/      # Host: ToolShareDbContext → schema "public"
├── ToolShare.HttpApi/                  # ABP Account/Identity controllers (kept)
├── ToolShare.DbMigrator/               # Composition root for migrations + seeding (both contexts)
├── ToolShare.Blazor/                   # Host web app — InteractiveServer only
│
├── ToolShare.Catalog.Domain.Shared/    # ToolCondition, ToolInstanceCirculationState, consts, L10n
├── ToolShare.Catalog.Domain/           # Category, Tool, ToolInstance, Photo, StateChange,
│                                       #   ToolInstanceManager, CategoryManager, repo interfaces,
│                                       #   CatalogDataSeedContributor (starter categories)
├── ToolShare.Catalog.Application.Contracts/  # PUBLIC BOUNDARY: IToolInstanceLookupAppService,
│                                       #   ToolInstanceStateChangedEto, CatalogPermissions,
│                                       #   ICategoryAppService, IToolAppService, IToolInstanceAppService, DTOs
├── ToolShare.Catalog.Application/      # App services, CatalogPhotoOptions, mapping profile
├── ToolShare.Catalog.EntityFrameworkCore/     # CatalogDbContext → schema "catalog",
│                                       #   EF configs, repositories, ICatalogDbSchemaMigrator
└── ToolShare.Catalog.Blazor/           # Razor pages/components + menu contributor

test/
├── ToolShare.TestBase/                 # Shared ABP test base + PostgreSqlContainerFixture
├── ToolShare.Domain.Tests/             # Host domain tests
├── ToolShare.EntityFrameworkCore.Tests/# Host EF tests (retargeted to Testcontainers)
├── ToolShare.Application.Tests/        # Host application tests
├── ToolShare.Catalog.Domain.Tests/     # Pure domain rules — no database
└── ToolShare.Catalog.Application.Tests/# Integration tests on real PostgreSQL (Testcontainers)
```

**Removed from the generated template** (InteractiveServer-only, no WebAssembly client):
`src/ToolShare.Blazor.Client`, `src/ToolShare.HttpApi.Client`, `test/ToolShare.HttpApi.Client.ConsoleTestApp`.

**Structure Decision**: Modular monolith in a single solution. The ABP open-source layered `app` template supplies the host (identity, OpenIddict, permission management, Blazor shell, DbMigrator), and Catalog is added as six sibling projects under `src/` following ABP module conventions rather than via the standalone `-t module` template — that template produces its own host/demo/test apps, which would duplicate infrastructure the monolith already owns. The `Catalog.*` prefix makes the boundary visible in the file tree, and every later module (Membership, Lending, …) repeats the same six-project shape. **[verified]** during Phase 0: `abp new ToolShare -t app -u blazor-webapp -d ef --dbms postgresql -v 10.5.0` produces `net10.0` projects with exactly the host layout above — the ABP 10.5 layout is byte-for-byte the same set of projects as 9.3.7, so the retarget cost no structural change.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| DbMigrator published into the `app` image and invoked by its entrypoint (rather than its own compose service) | The feature explicitly requires exactly two compose services (`app`, `postgres`) while FR-015/Principle VI require a dedicated, separate migration step. Bundling the DbMigrator binary and running it *before* the host starts keeps both true, and lets an operator run migrations alone via `docker compose run --rm`. | A third `dbmigrator` run-to-completion service exceeds the stated two-service shape; applying migrations from inside the host at startup would make the running application mutate its own schema, violating Principle VI outright. |
| Extra `ToolShare.Catalog.Application.Tests` + shared `ToolShare.TestBase` container fixture (7 test projects total) | Principle V mandates database-free domain tests *and* real-PostgreSQL application tests; those need different hosts, so they cannot share one project. The fixture lives in `ToolShare.TestBase` so the host and every future module reuse a single Testcontainers implementation instead of copying it per module. | One combined test project would force the fast domain tests to boot a container; duplicating the fixture per module would multiply the cost with each new module. |
| Persisted `NormalizedName` / `NormalizedSerialNumber` columns alongside the display values | SC-008 requires case-insensitive **and** accent-tolerant search (incl. Ukrainian), and FR-003 requires catalog-wide serial uniqueness that is not defeated by casing. A normalized, indexed column makes both a deterministic, unit-testable pure domain function and keeps the unique index enforceable at the database level. | `ILIKE` alone handles case but not accents. PostgreSQL `unaccent`/`citext` requires `CREATE EXTENSION` at migration time (elevated privileges, weaker portability across managed Postgres and the Testcontainers image) and moves the rule out of the unit-testable domain layer. |

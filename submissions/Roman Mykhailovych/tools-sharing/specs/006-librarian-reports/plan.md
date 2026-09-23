# Implementation Plan: Librarian Reports

**Branch**: `006-librarian-reports` | **Date**: 2026-08-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-librarian-reports/spec.md`

## Summary

Deliver the three librarian reports the product spec named in User Story 7 and that 004 and 005 both
explicitly deferred: **current overdue loans**, **most-borrowed tools**, and **maintenance cost over a
period**. All three are read-only projections over history Lending already keeps immutable (004
FR-026); this feature persists nothing new.

The single decision that shapes everything else, argued in [research.md](./research.md) R1: **this is a
new feature slice inside the existing Lending module, not a fifth module.** Two of the three reports
read only Lending's own tables. The third counts Lending's loans and needs only tool *identity*, which
Catalog already publishes through its frozen Tier 1 `IToolInstanceLookupAppService`. A
`ToolShare.Reports.*` module would own no data at all, and every way of feeding it — republishing
Lending's history as events into a duplicated read model, or adding report-shaped query methods to
Lending's contracts and passing them through — is strictly more machinery for the same output. The
constitution's Governance section asks plans to prefer the simpler option that keeps module boundaries
intact; here the simpler option *is* the boundary-preserving one.

Consequently this feature adds **no project, no package, no PostgreSQL schema, and no EF Core
migration**. It adds one `Reports/` slice to three existing Lending projects
(`Application.Contracts`, `Application`, `Blazor`), one permission, and two pure methods on the
existing `Loan` entity.

Three further decisions worth surfacing here:

1. **The overdue report computes overdue live; it does not read `Loan.IsOverdue`** (research R2).
   That flag is set by `OverdueMarkingWorker` on a **[verified]** 1-hour period, so it lags reality by
   up to an hour — unacceptable against FR-005's "MUST reflect the current moment". The computed
   predicate (`ReturnedAt IS NULL AND PlannedReturnDate < today`) is a strict superset of the flag-based
   one, so the report never shows *fewer* loans than the existing roster view, only the ones the sweep
   has not reached yet.
2. **Popularity aggregates by instance in SQL, then folds instances into tools in memory** (research
   R4). `Loan` stores `ToolInstanceId` only — by design, since Principle III forbids Lending holding a
   copy of Catalog's instance→tool relationship. The fold is bounded by the *fleet size* (low hundreds),
   not by loan history, so it does not grow as the community accumulates loans.
3. **Report queries use the default repository `IRepository<T, Guid>`**, not new methods on the shipped
   `ILoanRepository`/`IMaintenanceRequestRepository` (research R3) — matching the project's standing
   preference for default repositories in new query code, and leaving 004's tested repository surface
   untouched.

Tests follow Principle V: the two pure additions to `Loan` (`IsOverdueAsOf`, `DaysOverdueAsOf`) are unit
tested with no database, and all three reports are integration tested against real PostgreSQL through
the existing `PostgreSqlContainerFixture` — including a drift test asserting the SQL overdue filter
selects exactly the set `Loan.IsOverdueAsOf` selects in memory.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`), SDK pinned by `global.json`

**Primary Dependencies**: ABP Framework 10.5.0 (free/open-source only). **No new NuGet package and no
new `ProjectReference` anywhere.** **[verified]** `ToolShare.Lending.Application` already references
both `ToolShare.Catalog.Application.Contracts` and `ToolShare.Membership.Application.Contracts` (its
`.csproj`), which is the entire cross-module surface the reports need:
`IToolInstanceLookupAppService.GetByIdsAsync` for tool identity and
`IMemberStandingAppService.GetByIdsAsync` for member display names.

**Storage**: PostgreSQL 16, single database `toolshare`. **No new schema and no new migration.** All
three reports read `lending.Loans` and `lending.MaintenanceRequests` exactly as 004 shipped them —
`Loan.CheckedOutAt`/`PlannedReturnDate`/`ReturnedAt`, `MaintenanceRequest.Status`/`ClosedAt`/`Cost`.
No new index either, justified against this project's stated scale in research R7.

**Testing**: xUnit 2.9.3 + Shouldly 4.3 + NSubstitute 5.3; `Testcontainers.PostgreSql` 4.13.0 via the
shared `PostgreSqlContainerFixture`. New tests land in the existing
`ToolShare.Lending.Domain.Tests` (pure) and `ToolShare.Lending.Application.Tests` (integration)
projects — no new test project. No SQLite/in-memory provider anywhere.

**Build tooling**: unchanged. `dotnet-ef` is **not** needed for this feature — there is no migration to
scaffold.

**Target Platform**: Linux containers via `docker compose` (app + postgres); developer machines
Windows/macOS/Linux through the `dotnet` CLI.

**Project Type**: Modular-monolith web application — single ASP.NET Core host serving a server-rendered
Blazor UI (MudBlazor theme), composed of ABP modules.

**Performance Goals**: SC-001 gives a 1-minute budget per report from a librarian's perspective, which
is generous by two orders of magnitude for the query shapes involved. Concretely: each report is one
aggregate query over a Lending table plus at most one batch lookup into Catalog and one into
Membership — p95 well under 200 ms at the scale below. The popularity report's in-memory fold is over
*distinct instances appearing in loans*, bounded by fleet size (low hundreds), never by loan count.

**Constraints**: read-only — no report may create, modify, or delete a record (FR-011); no cross-schema
foreign keys and no SQL statement spanning schemas (every query touches only `lending`; Catalog and
Membership data arrives in-process through their published app services); no cross-module references
outside `*.Application.Contracts` + `ILocalEventBus`; ABP Commercial forbidden; must build and test
through the `dotnet` CLI with no IDE dependency.

**Scale/Scope**: Single community, single tenant, tens of concurrent users, low hundreds of members and
tool instances — matching every prior module's stated scale. This feature delivers **0** new projects,
**0** new entities, **0** new migrations: one app-service interface with three methods plus its DTOs,
one app-service implementation, one permission constant + its definition and localization, two pure
methods on the existing `Loan`, one Blazor page (three tabbed report panels), one menu item, and one
line added to the Librarian role seeder.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [.specify/memory/constitution.md](../../.specify/memory/constitution.md) v2.0.0.

| # | Principle | Verdict | How this plan satisfies it |
|---|-----------|---------|----------------------------|
| I | Fixed Stack (NON-NEGOTIABLE) | **PASS** | Same .NET 10 / ABP 10.5.0 / Blazor InteractiveServer / PostgreSQL 16 stack. Zero new packages — the strongest form of this gate any feature in this project has passed. |
| II | Modular Monolith With Strict Boundaries | **PASS** | The reports live inside Lending and reference no other module's `Domain`/`EntityFrameworkCore`. The two cross-module reads — Catalog's `IToolInstanceLookupAppService` and Membership's `IMemberStandingAppService` — are both already-frozen Tier 1 contracts that `LoanAppService`/`MyLendingAppService` already consume today (**[verified]**, same `.csproj` references, no new coupling direction and no new coupling edge). |
| III | Schema-Level Data Isolation; No Cross-Module FKs | **PASS** | No new table, so no new FK of any kind. Every SQL statement this feature issues touches only the `lending` schema; the instance→tool relationship the popularity report needs is obtained by calling Catalog's app service in process, never by joining `lending` to `catalog` (research R4). |
| IV | Append-Only History | **PASS** | Trivially — FR-011 makes the whole feature read-only, and the implementation adds no mutating code path. The reports exist *because* 004 kept its history append-only (004 FR-026); this feature is that guarantee being cashed in. |
| V | Test-First Discipline (NON-NEGOTIABLE) | **PASS** | `Loan.IsOverdueAsOf`/`DaysOverdueAsOf` are pure and unit tested with **no** database in `ToolShare.Lending.Domain.Tests`. All three reports, the permission boundary, and the date-range rejection are integration tested against real PostgreSQL via the existing Testcontainers fixture — including the SQL-vs-domain drift test described in research R2. |
| VI | IDE-Agnostic, Container-First | **PASS** | Adds no test project and **no migration** — `dotnet build` / `dotnet test` cover it with no new step, and `DbMigrator` is untouched because there is no schema change to apply. |

**Technology & Architecture Constraints check**: authorization uses ABP's built-in permission system —
one new permission, `Lending.Reports`, defined through `PermissionDefinitionProvider` exactly like
Lending's existing four — **PASS**. No background work is introduced, so the Background
Workers/Jobs rule is not engaged — **PASS**. The pure parts (what counts as overdue right now, and by
how many days) live in `ToolShare.Lending.Domain` on the `Loan` entity, free of EF Core and ABP
infrastructure — **PASS**.

The constraint that deserves explicit argument is: *"Read models that aggregate data are rebuilt by
handling domain events, not by database triggers or cross-schema queries."* This feature builds **no
read model** in the sense that sentence governs — nothing is materialized, stored, or kept in sync.
Each report is computed on demand from the module's own live tables and discarded. The two mechanisms
the sentence forbids are both absent: there is no database trigger, and no query crosses a schema. The
sentence constrains *how a persisted projection stays current*; a feature with no persisted projection
does not engage it. Recorded here rather than in Complexity Tracking because it is a non-violation, not
a justified one — **PASS**.

**Development Workflow check**: this feature defines its `Application.Contracts` surface (the report
app service and its DTOs) in the same feature as its only consumer, Lending's own Blazor UI — which is
the correct ordering for a Tier 2, module-internal surface with no downstream module, exactly as 004's
`ILoanAppService` and 005's `IMyNotificationsAppService` were handled — **PASS**. This plan adds no
functional requirements; those stay in [spec.md](./spec.md) — **PASS**.

### Boundary notes (recorded, not violations)

1. **This is the first feature that adds no new module.** 002 through 005 each introduced one. That is
   not a regression in ambition but the correct read of what this feature is: three queries over
   history that already exists, in the module that already owns it. Introducing `ToolShare.Reports.*`
   to hold zero data would satisfy a symmetry the constitution never asks for while adding a coupling
   edge (Reports → Lending's contracts) it would rather not have (research R1).
2. **This feature touches an already-shipped entity (`Loan`) for the second time in the project's
   history** — after 005 touched Membership's `MemberStandingDto`. The change here is narrower still:
   two `public` **query** methods (`IsOverdueAsOf`, `DaysOverdueAsOf`) that read existing fields and
   mutate nothing. No property, no constructor, no state transition, and therefore no schema impact
   and no possibility of behavioral regression in 004's tested paths.
3. **The existing `ILoanAppService.GetListAsync(OnlyOverdue: true)` is deliberately left alone.** It
   is 004's operational roster view, keyed off the stored `IsOverdue` flag, and 004's tests assert that
   behavior (`OverdueListFilterTests` drives the worker explicitly before asserting). The report is a
   *different* question asked of the same data — "who is overdue right now", not "which loans has the
   sweep marked" — so it gets its own live-computed query rather than a change to a shipped, tested
   method (research R2).
4. **No index is added** (research R7). At this project's stated scale the three aggregate queries are
   sub-millisecond seq scans; adding indexes speculatively would mean the migration this feature
   otherwise does not need. Adding them later is a non-breaking, migration-only change if the fleet
   ever outgrows the assumption.

**Post-Phase 1 re-evaluation**: re-run after [data-model.md](./data-model.md) and
[contracts/](./contracts/) were written — all six verdicts still PASS. The design introduced no entity,
no table, no FK, and no mutating operation, so Principles III and IV are if anything less engaged than
at the pre-design check. The one element that warranted a second look — adding methods to the shipped
`Loan` aggregate — was re-checked against Principle II and recorded as boundary note 2: both methods
are pure reads over fields `Loan` already owns, they are consumed only inside Lending, and the
compile-time dependency graph is unchanged in both direction and edge count.

## Project Structure

### Documentation (this feature)

```text
specs/006-librarian-reports/
├── plan.md                                   # This file (/speckit-plan output)
├── research.md                               # Phase 0 output — 8 decisions with alternatives
├── data-model.md                             # Phase 1 output — 3 projections, 0 new entities
├── quickstart.md                             # Phase 1 output — run & validate the slice
├── contracts/                                # Phase 1 output
│   ├── README.md                             # Index, stability tiers, placement rules
│   ├── lending-reports-app-service.md        # The Tier 2 service surface (UI contract)
│   └── lending-reports-permissions.md        # FR-012 — the one new permission and its grants
├── checklists/
│   └── requirements.md                       # Pre-existing spec quality checklist (16/16)
└── tasks.md                                  # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── ToolShare.Lending.Domain/
│   └── Loans/Loan.cs                          # CHANGED — + IsOverdueAsOf(DateOnly),
│                                              #   DaysOverdueAsOf(DateOnly); pure, no new state
│
├── ToolShare.Lending.Domain.Shared/
│   ├── LendingDomainErrorCodes.cs             # CHANGED — + InvalidReportDateRange
│   └── Localization/Lending/en.json           # CHANGED — + error text, permission name, menu entry
│
├── ToolShare.Lending.Application.Contracts/
│   ├── Reports/IReportAppService.cs           # NEW — 3 methods + input/output DTOs (co-located,
│   │                                          #   matching IMaintenanceRequestAppService's shape)
│   └── Permissions/                           # CHANGED — LendingPermissions.Reports.Default and
│                                              #   its definition-provider entry
│
├── ToolShare.Lending.Application/
│   └── Reports/ReportAppService.cs            # NEW — the three queries; injects
│                                              #   IRepository<Loan,Guid>, IRepository<MaintenanceRequest,Guid>,
│                                              #   IToolInstanceLookupAppService, IMemberStandingAppService
│
├── ToolShare.Lending.Blazor/
│   ├── Pages/Lending/Reports.razor            # NEW — one page, three MudTabPanel reports
│   └── Menus/                                 # CHANGED — + "Reports" item behind Lending.Reports
│
├── ToolShare.Application/
│   └── Identity/LibrarianRoleDataSeedContributor.cs  # CHANGED — + Lending.Reports in
│                                              #   LibrarianLendingPermissions (Administrator
│                                              #   inherits it via the existing union)
│
└── ToolShare.Catalog.*/, ToolShare.Membership.*/,
    ToolShare.Notifications.*/, ToolShare.DbMigrator/  # UNCHANGED

test/
├── ToolShare.Lending.Domain.Tests/
│   └── Loans/LoanOverdueCalculationTests.cs   # NEW — pure, no database
├── ToolShare.Lending.Application.Tests/
│   ├── Reports/OverdueReportTests.cs          # NEW — incl. read-only assertion (FR-011)
│   ├── Reports/ReportDriftTests.cs            # NEW — the SQL-vs-domain drift guard (R2)
│   ├── Reports/PopularityReportTests.cs       # NEW — incl. retired-instance coverage (FR-003)
│   ├── Reports/PopularityRangeValidationTests.cs   # NEW — FR-009, US2-owned
│   ├── Reports/MaintenanceCostReportTests.cs  # NEW — incl. open-request exclusion (FR-008)
│   ├── Reports/MaintenanceCostRangeValidationTests.cs  # NEW — FR-009, US3-owned (own file so
│   │                                          #   US3 ships without US2)
│   └── Authorization/ReportAuthorizationTests.cs  # NEW — FR-012
└── ToolShare.TestBase/, all other test projects      # UNCHANGED
```

**Structure Decision**: A `Reports/` **feature slice** inside the existing Lending module, following
CLAUDE.md's "feature slices within a module" convention — the same shape as Lending's existing
`Reservations/`, `Loans/`, and `Maintenance/` slices, present in `Application.Contracts`, `Application`,
and `Blazor`. No slice is needed in `Domain` or `EntityFrameworkCore`: the feature adds no entity and no
repository, and its only domain-layer addition is two methods on `Loan`, which belong in the existing
`Loans/` slice rather than a new one. Nothing is added to `ToolShare.slnx`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No principle is violated and no gate required justification, so this table is empty by design — the
first feature in this project for which that is true. The one judgment call that could have gone the
other way (whether to introduce an ABP `Specification` to share the overdue predicate between domain
and SQL) was resolved *toward* the simpler option and is recorded as a rejected alternative in
[research.md](./research.md) R2, not as accepted complexity.

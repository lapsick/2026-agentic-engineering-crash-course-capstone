# Implementation Plan: Lending — Reservations, Checkout, Return & Maintenance

**Branch**: `004-lending` | **Date**: 2026-08-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-lending/spec.md`

## Summary

Add **Lending** as the third business module of the ToolShare modular monolith: a member reserves an
available instance or joins its FIFO waitlist, a Librarian checks it out and later records its return,
a worsened return automatically opens a maintenance request that a Librarian closes with a cost, and
every closed loan reports its reliability outcome (overdue, damage, clean) back to Membership. Reminders
and overdue marking run on scheduled background workers rather than ad-hoc timers, per the constitution.

The module ships as the same six projects Catalog and Membership use, with its own `LendingDbContext`
mapped to a new **`lending`** PostgreSQL schema. Four aggregates — `Reservation`, `WaitlistEntry`,
`Loan`, `MaintenanceRequest` — each simple enough (at most one meaningful terminal transition) that
none needs a `Member`/`ToolInstance`-style child history table (research R6); each satisfies
Constitution IV by never overwriting a field once set and never being deleted.

Two decisions shape most of the work and are argued in [research.md](./research.md):

1. **Catalog needs a second, narrower published capability this feature adds.** Catalog's existing
   `IToolInstanceLookupAppService` (002) is read-only; Lending needs to move an instance to on-loan/
   under-maintenance and back and to record its returned condition. A new inbound Tier 1 contract,
   `IToolInstanceCirculationReportingAppService`, mirrors the exact shape 003's
   `IReliabilityReportingAppService` established for the identical kind of problem one module hop
   earlier in the chain (R2). This is the first feature to modify an already-shipped module's
   *production* code rather than only its tests (R10) — additive only, so no existing consumer breaks.
2. **Background work uses ABP Background Workers, not ad-hoc timers**, per the constitution's explicit
   instruction. Three narrow periodic workers — waitlist-offer expiry, return reminders, overdue
   marking — reuse `AsyncPeriodicBackgroundWorkerBase` (**[verified]** already a transitive dependency
   of the host, no new package). `Loan.Id` doubles as the occurrence identifier Membership's
   reliability-reporting contract requires, so no separate identifier concept is invented (R4, R5).

Preventing two members from double-booking the same instance is a database-level PostgreSQL exclusion
constraint (`EXCLUDE USING gist`, `btree_gist`) on the date range — the same "constraint is the
authority under concurrency" idiom Membership's filtered unique index already established (R3).

Tests follow Principle V: pure domain rules (date-range containment, condition-ordering comparison,
FIFO ordering) without a database; application behaviour against real PostgreSQL through the existing
shared `PostgreSqlContainerFixture`, extended for the first time to compose Catalog's, Membership's,
**and** Lending's `EntityFrameworkCore` modules in one test module.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`), SDK pinned by `global.json`

**Primary Dependencies**: ABP Framework 10.5.0 (free/open-source only). New to this feature: **no new
NuGet packages** — Lending uses `Volo.Abp.Ddd.Domain`/`.Application`, `Volo.Abp.Authorization`,
`Volo.Abp.EntityFrameworkCore.PostgreSql`, and `Volo.Abp.BackgroundWorkers` (**[verified]** already a
transitive dependency of the host, currently unused by any feature — this is the first to activate it),
all already resolved into the solution.

**Storage**: PostgreSQL 16, single database `toolshare`; host schema `public`, Catalog schema
`catalog`, Membership schema `membership`, **new** Lending schema `lending` with its own
`lending.__EFMigrationsHistory`, plus the `btree_gist` extension this feature's migration enables for
the reservation-overlap exclusion constraint (research R3).

**Testing**: xUnit 2.9.3 + Shouldly 4.3 + NSubstitute 5.3; `Testcontainers.PostgreSql` 4.13.0 via the
shared `PostgreSqlContainerFixture` (one container per assembly, template database cloned per test
class). No SQLite/in-memory provider anywhere.

**Build tooling**: unchanged — `dotnet-ef` 10.x required for the new `Lending_Initial` migration (and
for the Catalog migration adding the two new `ToolInstanceCirculationState` enum values, which needs
no schema change since the column is already an `int`).

**Target Platform**: Linux containers via `docker compose` (app + postgres); developer machines
Windows/macOS/Linux through the `dotnet` CLI.

**Project Type**: Modular-monolith web application — single ASP.NET Core host serving a
server-rendered Blazor UI (MudBlazor theme), composed of ABP modules.

**Performance Goals**: reservation-eligibility check (availability + standing + overlap pre-check) p95
< 150 ms (it composes three live reads — Catalog's lookup, Membership's standing, Lending's own
overlap query — so it is inherently costlier than Membership's single-cache-read standing lookup, but
still an interactive-path operation); background worker sweeps (waitlist expiry, reminders, overdue
marking) complete a full pass in under 5 seconds at the scale below, run no more often than once per
minute.

**Constraints**: No cross-schema foreign keys; no cross-module references outside
`*.Application.Contracts` + `ILocalEventBus`; history-bearing fields never overwritten once set
(research R6); the running application never mutates its own schema; ABP Commercial forbidden; must
build and test through the `dotnet` CLI with no IDE dependency; background work uses ABP Background
Workers/Jobs only (constitution, Technology & Architecture Constraints).

**Scale/Scope**: Single community, single tenant, tens of concurrent users, low hundreds of members,
low hundreds of tool instances, expected low tens of concurrent open loans/reservations at any time.
This feature delivers 6 new module projects + 2 new test projects, 4 aggregates, 3 enums, 4 Lending
application services + 1 self-service service, 3 background workers, 1 new Catalog Tier 1 service +
2 new Catalog domain methods + 2 new Catalog enum values, ~5 Blazor pages, and updates to 3 existing
host/test areas (role seeder, Catalog's Application.Contracts/Application/Domain, test fixtures).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [.specify/memory/constitution.md](../../.specify/memory/constitution.md) v2.0.0.

| # | Principle | Verdict | How this plan satisfies it |
|---|-----------|---------|----------------------------|
| I | Fixed Stack (NON-NEGOTIABLE) | **PASS** | Same .NET 10 / ABP 10.5.0 / Blazor InteractiveServer / PostgreSQL 16 stack, zero new packages. `AsyncPeriodicBackgroundWorkerBase` and `IBackgroundWorkerManager` were **[verified]** present in the already-resolved `Volo.Abp.BackgroundWorkers.dll` (research R4). |
| II | Modular Monolith With Strict Boundaries | **PASS** | Lending ships as the six prescribed projects, references no other module's `Domain`/`EntityFrameworkCore`. Its two cross-module dependencies — calling Membership's `Application.Contracts` and Catalog's (extended) `Application.Contracts` — both flow through the one permitted channel. The one unusual element, Catalog gaining new published surface *for* Lending, is additive to Catalog's own `Application.Contracts` and implemented entirely inside Catalog — it does not create a new coupling direction, only a new capability on an existing, already-legal one (research R2, R10). |
| III | Schema-Level Data Isolation; No Cross-Module FKs | **PASS** | `LendingDbContext` owns schema `lending` with its own migrations history. The one FK-like relationship — `Loan.ReservationId`, `MaintenanceRequest.TriggeringLoanId` — are both **within** the `lending` schema (Lending referencing its own other aggregates), not cross-module. `MemberId`, `ToolInstanceId` are plain `uuid` columns with **no** FK, exactly like every cross-module reference in 002/003. |
| IV | Append-Only History | **PASS** | Every one of the four new aggregates satisfies this by construction (research R6): no field is overwritten once set (`CancelledAt`, `ReturnedAt`, `ClosedAt`, etc. are all write-once), and no row is ever deleted. `MaintenanceRequest.Cost` and `Loan.ReturnedCondition` remain permanently visible after the request/loan closes (FR-017, FR-012). |
| V | Test-First Discipline (NON-NEGOTIABLE) | **PASS** | Domain rules (date-range containment, condition-ordering "worse than," FIFO comparison) are unit-tested with **no** database. Application behaviour — the reservation/checkout/return/maintenance flow, the Catalog and Membership integrations, the exclusion constraint under real concurrent inserts, the background workers' idempotent sweeps — is integration-tested against real PostgreSQL via the existing Testcontainers fixture, extended to compose all three modules' `EntityFrameworkCore` layers for the first time. |
| VI | IDE-Agnostic, Container-First | **PASS** | Adds two `dotnet test` projects, one new EF migration (`Lending_Initial`, plus a small Catalog migration for the two new enum values — no schema change, since the backing column is already `int`), and no IDE-specific step. Migrations remain applied only by `DbMigrator`; the running app never mutates its own schema. |

**Technology & Architecture Constraints check**: authentication/authorization use ABP's built-in
`IdentityModule` + permission system, unchanged — **PASS**. Background work (waitlist-offer expiry,
return reminders, overdue marking) uses ABP Background Workers, per the constitution's explicit
instruction — **PASS**, and this is the *first* feature to actually exercise that constraint rather
than only satisfying it vacuously (002/003 had no background work). Pure domain algorithms
(date-range containment, condition-ordering comparison, FIFO ordering) live in
`ToolShare.Lending.Domain` free of EF Core and ABP infrastructure — **PASS**. No read models are
rebuilt from events by this feature; the background workers query their own module's repositories
directly rather than through cross-schema queries — **PASS**.

**Development Workflow check**: `ToolShare.Lending.Application.Contracts` is defined in this feature;
the `IToolInstanceCirculationReportingAppService` addition to Catalog's own `Application.Contracts` is
likewise defined before any consumer needs it (Lending is its only consumer, defined in the same
feature) — **PASS**. This plan adds no functional requirements; those stay in
[spec.md](./spec.md) — **PASS**.

### Boundary notes (recorded, not violations)

1. **This feature modifies an already-shipped module's production code — a first.** 002 and 003 both
   left every previously-shipped module's *production* code untouched (003 touched only Catalog's test
   project and one contract document). This feature's `catalog-extension.md` is additive-only to
   Catalog's Tier 1 surface (new enum values, new interface, new domain methods on an existing
   aggregate) and changes no existing method's behavior or signature — the Tier 1 stability rule 002
   established ("changes must be additive only") is met, not overridden.
2. **Lending sits on both ends of the "downstream reports a fact upstream" pattern 003 introduced.**
   It is a consumer of that pattern against Membership and, simultaneously, the reason Catalog gains a
   producer-side instance of it. Recording this explicitly because it is easy to mistake for a new
   architectural idea when it is in fact the same one applied twice.
3. **The exclusion constraint is enforced by a raw-SQL migration step**, since EF Core's fluent API has
   no first-class `EXCLUDE USING gist` builder. This is scoped to `LendingDbContext`'s own migration
   file, touches no other module's schema, and is the same category of "index/constraint as the
   concurrency authority" already used twice (Catalog's serial-number uniqueness, Membership's
   `(OccurrenceId, OutcomeType)` filtered unique index) — a new *mechanism* (`EXCLUDE` vs. `UNIQUE`),
   not a new *principle*.

**Post-Phase 1 re-evaluation**: re-run after [data-model.md](./data-model.md) and
[contracts/](./contracts/) were written — all six verdicts still PASS. The designed aggregates
introduce no cross-schema FK; every history-bearing field is write-once; the published surface (this
module's two events, Catalog's four new operations) exposes only interfaces, DTOs, and `Domain.Shared`
types. The one design element that warranted a second look — modifying Catalog's production code —
was re-checked against Principle II and the Tier 1 stability rule and is recorded as boundary note 1
rather than as a violation, because every change to Catalog is additive and the compile-time
dependency graph still flows Lending → Catalog's `Application.Contracts`, never the reverse.

## Project Structure

### Documentation (this feature)

```text
specs/004-lending/
├── plan.md                                 # This file (/speckit-plan output)
├── research.md                             # Phase 0 output — 11 decisions with alternatives
├── data-model.md                           # Phase 1 output — 4 aggregates, rules, transitions
├── quickstart.md                           # Phase 1 output — run & validate the slice
├── contracts/                              # Phase 1 output
│   ├── README.md                           # Index, stability tiers, placement rules
│   ├── lending-events.md                   # FR-022/023 — LendingNotificationDueEto
│   ├── lending-app-services.md             # Module-internal service surface (UI contract)
│   ├── lending-permissions.md              # FR-018/019/029/030 permission boundary
│   ├── membership-consumption.md           # Exactly how Lending calls Membership's contracts
│   └── catalog-extension.md                # The new Tier 1 surface this feature adds to Catalog
├── checklists/
│   └── requirements.md                     # Pre-existing spec quality checklist (16/16)
└── tasks.md                                # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── ToolShare.Lending.Domain.Shared/         # NEW — ReservationStatus, WaitlistOfferState,
│                                            #   MaintenanceRequestStatus, error codes, L10n,
│                                            #   LendingNotificationDueEto + LendingNotificationKind
├── ToolShare.Lending.Domain/                # NEW — Reservation, WaitlistEntry, Loan,
│                                            #   MaintenanceRequest, ReservationManager,
│                                            #   WaitlistManager, LoanManager, repo interfaces
├── ToolShare.Lending.Application.Contracts/ # NEW — IReservationAppService, IMyLendingAppService,
│                                            #   ILoanAppService, IMaintenanceRequestAppService,
│                                            #   LendingPermissions, DTOs
├── ToolShare.Lending.Application/           # NEW — app services, mappers, the three periodic
│                                            #   background workers, Catalog/Membership call sites
├── ToolShare.Lending.EntityFrameworkCore/   # NEW — LendingDbContext → schema "lending", EF configs,
│                                            #   repositories, the btree_gist/EXCLUDE migration step
├── ToolShare.Lending.Blazor/                # NEW — Reserve/waitlist UI, checkout/return screens,
│                                            #   maintenance queue, MyLending page, menu contributor
│
├── ToolShare.Catalog.Domain.Shared/         # CHANGED — ToolInstanceCirculationState gains
│                                            #   OnLoan/UnderMaintenance (additive)
├── ToolShare.Catalog.Domain/                # CHANGED — ToolInstance gains MarkOnLoan/Return/
│                                            #   ReturnForMaintenance/CloseMaintenance
├── ToolShare.Catalog.Application.Contracts/ # CHANGED — NEW IToolInstanceCirculationReportingAppService
├── ToolShare.Catalog.Application/           # CHANGED — its implementation
├── ToolShare.Application/                   # CHANGED — role seeder extended: Lending.* grants +
│                                            #   Catalog.ToolInstances.ReportLendingState for Librarian
├── ToolShare.Blazor/                        # CHANGED — Lending module deps + project references
├── ToolShare.DbMigrator/                    # CHANGED — depend on Lending EF + Application
└── ToolShare.Membership.*/                  # UNCHANGED — Lending consumes its published contracts
                                             #   exactly as shipped; zero changes required

test/
├── ToolShare.Lending.Domain.Tests/          # NEW — pure rules, no database
├── ToolShare.Lending.Application.Tests/     # NEW — integration on real PostgreSQL, incl.
│                                            #   PublicContract/ boundary tests
├── ToolShare.Catalog.Application.Tests/     # CHANGED — new tests for the circulation-reporting
│                                            #   contract; existing tests otherwise unaffected
├── ToolShare.TestBase/                      # UNCHANGED — fixture reused as-is
└── ToolShare.Membership.Application.Tests/  # UNCHANGED
```

**Structure Decision**: Repeat Catalog's and Membership's six-project module shape verbatim under the
`ToolShare.Lending.*` prefix, added as sibling projects in `ToolShare.slnx`. This is the third instance
of the shape, confirming rather than establishing it (research R1). The only files outside
`ToolShare.Lending.*` that change in a way that alters *behavior* are the four Catalog projects named
above (additive only, per the Tier 1 stability rule) and the host role seeder; Membership is touched
nowhere.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Modifying Catalog's production code (new enum values, new domain methods, a new Tier 1 service) | Lending needs to record facts about instances it does not own (on loan, under maintenance, condition on return) without violating Constitution III's schema isolation; only Catalog itself can mutate `ToolInstance`. | *Lending reaching into Catalog's schema directly* — a flat Constitution II/III violation. *Lending owning a shadow copy of circulation state* — would let Catalog's and Lending's views of "is this instance available" diverge, the exact class of bug schema isolation exists to prevent. |
| PostgreSQL `EXCLUDE` constraint (raw-SQL migration step) instead of a plain EF Core unique index | Overlap prevention for date *ranges* (FR-002, FR-007, SC-003) cannot be expressed as equality-based uniqueness; only a range-aware exclusion constraint enforces it at the database level under real concurrency. | *Application-level overlap check alone* — two concurrent transactions can both pass the check before either commits (the exact race SC-003 must have zero instances of). *Serializable isolation for reservation creation* — sufficient but forces a much heavier lock/retry profile for a problem the exclusion constraint already solves at default isolation. |
| Three new ABP periodic background workers (the first feature to use `Volo.Abp.BackgroundWorkers` for real) | FR-005 (offer-window expiry), FR-022 (reminders), FR-023 (overdue marking) are all "a moment in time arrives with nobody necessarily present" problems; the constitution names ABP Background Workers/Jobs as the required mechanism. | *Ad-hoc `Timer`/`Task.Delay` loops* — explicitly forbidden by the constitution. *A single combined worker* — rejected so each concern's period can be tuned independently (R5) without one slow query delaying the others. |
| No retry wrapper around Lending's own calls to Membership's reliability-reporting contract | Membership's own implementation (003) already absorbs concurrent-contention retries and guarantees it never surfaces `AbpDbConcurrencyException` to its caller. | *Adding a second retry loop anyway, defensively* — redundant complexity with no correctness benefit; if the underlying guarantee is ever violated, the fix belongs in Membership, not duplicated in every caller (research R8). |
| No `Member`/`ToolInstance`-style dedicated append-only child-history entity for any of the four new aggregates | None of the four has more than one meaningful terminal transition from its starting state — the combinatorial-transition-kind problem that motivated a shared history stream for `Member` (4 kinds) and `ToolInstance` (2 dimensions) does not exist here. | *Adding one anyway, for pattern consistency with 002/003* — rejected as literal, unjustified complexity the constitution's own governance section asks to avoid absent a concrete requirement (research R6); each entity's write-once fields already satisfy Constitution IV. |

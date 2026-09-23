---

description: "Task list for Lending — Reservations, Checkout, Return & Maintenance"
---

# Tasks: Lending — Reservations, Checkout, Return & Maintenance

**Input**: Design documents from `/specs/004-lending/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: MANDATORY. Constitution **Principle V (Test-First Discipline, NON-NEGOTIABLE)** requires xUnit
domain unit tests (no database) plus application-layer integration tests against real PostgreSQL via
Testcontainers. Test tasks are listed **before** the implementation they cover and must fail first.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested and demoed
independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Exact file paths are included in every task

## Path Conventions

Modular monolith at repository root per [plan.md](./plan.md): `src/ToolShare.Lending.*` for the new
module, `src/ToolShare.Catalog.*` for the module this feature additively extends, `src/ToolShare.*`
for the host, `test/ToolShare.Lending.*` for its tests. Stack: **.NET 10 (`net10.0`) / ABP 10.5.0 /
PostgreSQL 16 / Blazor Web App (InteractiveServer, MudBlazor)**. No new NuGet packages are introduced
by this feature — `Volo.Abp.BackgroundWorkers` is already a transitive dependency (research R4).

> **Read before starting**: [research.md](./research.md) R2 (the Catalog extension), R3 (the exclusion
> constraint), R6 (why no child-history table), R7 (the reservation-cancellation cascade), R8 (no local
> retry around Membership reporting), and R10 (the scope of the Catalog ripple). Unlike 003's ripple on
> Catalog (test-only), this feature's ripple touches Catalog's **production** code — additive only, so
> no existing Catalog test should ever need to change.

---

## Phase 1: Setup (Module Skeleton)

**Purpose**: Create the six module projects and two test projects, wire the ABP module dependency
graph, and confirm the empty skeleton builds

- [X] T001 Create `src/ToolShare.Lending.Domain.Shared/` (`net10.0` classlib) with `LendingDomainSharedModule.cs` depending on `AbpValidationModule`, and add it to `ToolShare.slnx`
- [X] T002 Create `src/ToolShare.Lending.Domain/` with `LendingDomainModule.cs` depending on `LendingDomainSharedModule` + `AbpDddDomainModule`, referencing `ToolShare.Lending.Domain.Shared`, and add it to `ToolShare.slnx`
- [X] T003 Create `src/ToolShare.Lending.Application.Contracts/` with `LendingApplicationContractsModule.cs` depending on `LendingDomainSharedModule` + `AbpDddApplicationContractsModule` + `AbpAuthorizationModule`, and add it to `ToolShare.slnx`
- [X] T004 Create `src/ToolShare.Lending.Application/` with `LendingApplicationModule.cs` depending on `LendingDomainModule` + `LendingApplicationContractsModule` + `AbpDddApplicationModule` + `AbpBackgroundWorkersModule` + `ToolShare.Catalog.Application.Contracts` + `ToolShare.Membership.Application.Contracts` (the two modules Lending calls into), and add it to `ToolShare.slnx`
- [X] T005 Create `src/ToolShare.Lending.EntityFrameworkCore/` with `LendingEntityFrameworkCoreModule.cs` depending on `LendingDomainModule` + `AbpEntityFrameworkCorePostgreSqlModule`, and add it to `ToolShare.slnx`
- [X] T006 Create `src/ToolShare.Lending.Blazor/` (`Microsoft.NET.Sdk.Razor`, `AddRazorSupportForMvc`) with `LendingBlazorModule.cs` depending on `LendingApplicationContractsModule` + `AbpAspNetCoreComponentsWebModule`, referencing `Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor` and `Volo.Abp.UI.Navigation`, mirroring `src/ToolShare.Membership.Blazor/ToolShare.Membership.Blazor.csproj`, and add it to `ToolShare.slnx`
- [X] T007 [P] Create `test/ToolShare.Lending.Domain.Tests/` (xUnit + Shouldly, references `ToolShare.Lending.Domain`, **no** database packages) and add it to `ToolShare.slnx`
- [X] T008 [P] Create `test/ToolShare.Lending.Application.Tests/` (xUnit + Shouldly + NSubstitute + `Testcontainers.PostgreSql`, references `ToolShare.Lending.Application`, `ToolShare.Lending.EntityFrameworkCore`, `ToolShare.Catalog.EntityFrameworkCore`, `ToolShare.Membership.EntityFrameworkCore`, `ToolShare.EntityFrameworkCore`, `ToolShare.TestBase`) and add it to `ToolShare.slnx`
- [X] T009 Add `ProjectReference`s and `[DependsOn]` entries for `LendingApplicationModule` + `LendingEntityFrameworkCoreModule` to `src/ToolShare.Blazor/ToolShare.Blazor.csproj` / `ToolShareBlazorModule.cs`, `LendingBlazorModule` to the same, and to `src/ToolShare.DbMigrator/ToolShare.DbMigrator.csproj` / `ToolShareDbMigratorModule.cs`
- [X] T010 Verify the skeleton compiles: `dotnet build ToolShare.slnx` succeeds with 0 errors

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared types, domain model, persistence, the Catalog extension, and the test harness
every user story builds on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Domain.Shared — published primitives

- [X] T011 [P] Create `ReservationStatus` enum in `src/ToolShare.Lending.Domain.Shared/ReservationStatus.cs` with the exact numeric values from [data-model.md](./data-model.md)
- [X] T012 [P] Create `WaitlistOfferState` enum in `src/ToolShare.Lending.Domain.Shared/WaitlistOfferState.cs`
- [X] T013 [P] Create `MaintenanceRequestStatus` enum in `src/ToolShare.Lending.Domain.Shared/MaintenanceRequestStatus.cs`
- [X] T014 [P] Create `LendingNotificationDueEto` and `LendingNotificationKind` in `src/ToolShare.Lending.Domain.Shared/Loans/LendingNotificationDueEto.cs` exactly as specified in [contracts/lending-events.md](./contracts/lending-events.md) — lives here, **not** in `Application.Contracts`, because `Domain` raises it via `AddLocalEvent`
- [X] T015 [P] Create `LendingDomainErrorCodes` in `src/ToolShare.Lending.Domain.Shared/LendingDomainErrorCodes.cs` covering every code in [contracts/lending-app-services.md](./contracts/lending-app-services.md#error-codes-toolsharelendingdomainshared)
- [X] T016 [P] Create `LendingResource` + `Localization/Lending/en.json` in `src/ToolShare.Lending.Domain.Shared/` with a message for every error code, and register it in `LendingDomainSharedModule`
- [X] T017 [P] Create `LendingDomainSharedConsts` (field lengths — e.g. `CancellationReasonMaxLength`) in `src/ToolShare.Lending.Domain.Shared/`

### Domain unit tests — write first, must fail

> These cover the pure rules in [data-model.md](./data-model.md). No database, no ABP infrastructure.

- [X] T018 [P] Write `ReservationLifecycleTests` in `test/ToolShare.Lending.Domain.Tests/Reservations/ReservationLifecycleTests.cs` covering `RES-01` (term-length and date-ordering guard), `RES-06` (cancel only while `Active`), `RES-07` (realize-as-checked-out is one-way), `RES-08` (cancel-for-maintenance only while `Active` and not yet started)
- [X] T019 [P] Write `WaitlistEntryLifecycleTests` in `test/ToolShare.Lending.Domain.Tests/Reservations/WaitlistEntryLifecycleTests.cs` covering `WL-03` (offer guard), `WL-04` (confirm only while `Offered` and within the window), `WL-05` (expire only while `Offered` and past the window)
- [X] T020 [P] Write `LoanLifecycleTests` in `test/ToolShare.Lending.Domain.Tests/Loans/LoanLifecycleTests.cs` covering `LOAN-01` (create requires an `Active` reservation and realizes it), `LOAN-03` (return only while open), `LOAN-04`'s "worse than" comparison across every pair on the 4-level scale (New/Good/Worn/Damaged)
- [X] T021 [P] Write `MaintenanceRequestLifecycleTests` in `test/ToolShare.Lending.Domain.Tests/Maintenance/MaintenanceRequestLifecycleTests.cs` covering `MAINT-02` (close only while `Open`, cost ≥ 0, zero is valid and distinct from omitted)

### Domain — entities, services, ports

- [X] T022 Create the `Reservation` aggregate root in `src/ToolShare.Lending.Domain/Reservations/Reservation.cs` implementing `RES-01`, `RES-06`, `RES-07`, `RES-08`
- [X] T023 [P] Create the `WaitlistEntry` aggregate root in `src/ToolShare.Lending.Domain/Reservations/WaitlistEntry.cs` implementing `WL-01`, `WL-03`, `WL-04`, `WL-05`
- [X] T024 Create the `Loan` aggregate root in `src/ToolShare.Lending.Domain/Loans/Loan.cs` implementing `LOAN-01`–`LOAN-06`
- [X] T025 Create the `MaintenanceRequest` aggregate root in `src/ToolShare.Lending.Domain/Maintenance/MaintenanceRequest.cs` implementing `MAINT-01`, `MAINT-02`
- [X] T026 [P] Create `IReservationRepository`, `IWaitlistEntryRepository`, `ILoanRepository`, `IMaintenanceRequestRepository` in `src/ToolShare.Lending.Domain/Reservations/`, `Loans/`, `Maintenance/` with only the methods listed in [data-model.md](./data-model.md#repositories-interfaces-in-toolsharelendingdomain)
- [X] T027 Create `ReservationManager` in `src/ToolShare.Lending.Domain/Reservations/ReservationManager.cs` enforcing `RES-02`–`RES-05` (needs repository access plus the caller-supplied Membership standing/rules) and `RES-08`/`MAINT-03`'s cancellation cascade
- [X] T028 Create `WaitlistManager` in `src/ToolShare.Lending.Domain/Reservations/WaitlistManager.cs` enforcing `WL-02` (join) and driving `WL-03` (offer the earliest waiting entry whenever an instance frees)
- [X] T029 Create `LoanManager` in `src/ToolShare.Lending.Domain/Loans/LoanManager.cs` enforcing `LOAN-06` (availability re-check at checkout) and orchestrating `LOAN-04`'s maintenance-request opening in the same operation as a worsened return
- [X] T030 Run `dotnet test test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj` and confirm every test from T018–T021 now passes

### EntityFrameworkCore — persistence

- [X] T031 [P] Create `LendingDbProperties` in `src/ToolShare.Lending.EntityFrameworkCore/EntityFrameworkCore/LendingDbProperties.cs` (`DbSchema = "lending"`, `DbTablePrefix = ""`, `ConnectionStringName = "Default"`)
- [X] T032 Create `LendingDbContext` in `src/ToolShare.Lending.EntityFrameworkCore/EntityFrameworkCore/LendingDbContext.cs` with `[ConnectionStringName("Default")]`, `HasDefaultSchema(LendingDbProperties.DbSchema)` and `DbSet`s for `Reservations`, `WaitlistEntries`, `Loans`, `MaintenanceRequests`
- [X] T033 Create `LendingDbContextModelCreatingExtensions` in `src/ToolShare.Lending.EntityFrameworkCore/EntityFrameworkCore/LendingDbContextModelCreatingExtensions.cs` with every index from [data-model.md](./data-model.md#indexes-and-constraints-summary) — the filtered unique index on `WaitlistEntries` (`WL-02`) and on `MaintenanceRequests` (`MAINT-01`) via EF's fluent API, and **no FK leaving the `lending` schema**
- [X] T034 [P] Create `EfCoreReservationRepository`, `EfCoreWaitlistEntryRepository`, `EfCoreLoanRepository`, `EfCoreMaintenanceRequestRepository` in `src/ToolShare.Lending.EntityFrameworkCore/Repositories/`, registering them via `AddDefaultRepositories(includeAllEntities: true)` plus the custom interfaces
- [X] T035 [P] Create `LendingDbContextFactory` in `src/ToolShare.Lending.EntityFrameworkCore/EntityFrameworkCore/LendingDbContextFactory.cs` for design-time tooling, mirroring `MembershipDbContextFactory`
- [X] T036 Generate the `Add_Lending_Schema` migration with `dotnet ef migrations add Add_Lending_Schema` (startup project `src/ToolShare.DbMigrator`, `dotnet-ef` **10.x**), then hand-edit the generated migration to add `migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;")` and the `EXCLUDE USING gist (\"ToolInstanceId\" WITH =, daterange(\"StartDate\", \"EndDate\", '[]') WITH &&) WHERE (\"Status\" = 0)` constraint on `Reservations` (research R3) — **deviation**: on inspection, Catalog's and Membership's own migrations were already consolidated into `src/ToolShare.EntityFrameworkCore/Migrations/` against `ToolShareDbContext` (via `[ReplaceDbContext]`), not kept in each module's own `EntityFrameworkCore/Migrations/` folder as originally planned — the same "followed the code, not the task text" call 003 made for its own T037/R11. `LendingDbContext`/`ILendingDbContext` were still created (for standalone module use and the EF repository generic parameter), but `ToolShareDbContext` now also implements `ILendingDbContext`, adds the four `DbSet`s, and calls `builder.ConfigureLending()`; the migration therefore lives in `src/ToolShare.EntityFrameworkCore/Migrations/20260803161348_Add_Lending_Schema.cs`. Verified: schema `lending` created with its own tables under the host's single `__EFMigrationsHistory`, the `btree_gist` extension enabled, and the exclusion constraint present (`\d lending."Reservations"`)

### Catalog extension (research R2, R10) — additive, must not break any existing Catalog test

- [X] T037 [P] Add `OnLoan = 2` and `UnderMaintenance = 3` to `ToolInstanceCirculationState` in `src/ToolShare.Catalog.Domain.Shared/ToolInstanceCirculationState.cs`, updating its doc comment to record that this feature is what introduces them
- [X] T038 [P] Write `ToolInstanceCirculationTests` in `test/ToolShare.Catalog.Domain.Tests/ToolInstances/ToolInstanceCirculationTests.cs` covering the four new `ToolInstance` methods' guards (mirroring the existing `ChangeCondition`/`Retire` test coverage) — write first, must fail
- [X] T039 Add `MarkOnLoan()`, `Return(ToolCondition condition)`, `ReturnForMaintenance(ToolCondition condition)`, `CloseMaintenance()` methods to `ToolInstance` in `src/ToolShare.Catalog.Domain/ToolInstances/ToolInstance.cs`, each following `ChangeCondition`/`Retire`'s exact shape: guard on current `CirculationState`, mutate `Condition`/`CirculationState`, append to the existing `StateHistory` via `AppendHistory`, raise the existing `ToolInstanceStateChangedEto` via `RaiseStateChangedEvent` — confirm T038 now passes
- [X] T040 [P] Create `IToolInstanceCirculationReportingAppService` in `src/ToolShare.Catalog.Application.Contracts/ToolInstances/IToolInstanceCirculationReportingAppService.cs` exactly as specified in [contracts/catalog-extension.md](./contracts/catalog-extension.md) — no new DTOs needed beyond primitive parameters
- [X] T041 [P] Add `Catalog.ToolInstances.ReportLendingState` to `CatalogPermissions` and `CatalogPermissionDefinitionProvider` in `src/ToolShare.Catalog.Application.Contracts/Permissions/`
- [X] T042 Implement `ToolInstanceCirculationReportingAppService` in `src/ToolShare.Catalog.Application/ToolInstances/ToolInstanceCirculationReportingAppService.cs`, gated by `Catalog.ToolInstances.ReportLendingState`
- [X] T043 Grant `Catalog.ToolInstances.ReportLendingState` to `Librarian` in `src/ToolShare.Application/Identity/LibrarianRoleDataSeedContributor.cs` (cumulative to `Administrator` automatically, via `MembershipRoleDataSeedContributor`'s existing `Concat`)
- [X] T044 [P] Write `CirculationReportingContractTests` in `test/ToolShare.Catalog.Application.Tests/ToolInstances/CirculationReportingContractTests.cs` covering every operation and rejection in [contracts/catalog-extension.md](./contracts/catalog-extension.md)'s table, plus permission enforcement — write first, must fail
- [X] T045 [P] Amend `specs/002-catalog-foundation/contracts/README.md`'s stability-tier table and `catalog-public-contracts.md` to record the new Tier 1 `IToolInstanceCirculationReportingAppService` surface and the two new `ToolInstanceCirculationState` values, with a pointer to this feature — mirrors 003's own amendment of `catalog-permissions.md`
- [X] T046 Run `dotnet test test/ToolShare.Catalog.Domain.Tests/…` and `test/ToolShare.Catalog.Application.Tests/…`, confirm T038/T044 pass and every pre-existing Catalog test remains green (the additive-only guarantee, research R10)

### Test harness

- [X] T047 Create `LendingApplicationTestFixture` + `LendingApplicationTestCollection` in `test/ToolShare.Lending.Application.Tests/`, mirroring `CatalogApplicationTestFixture` — one Testcontainers PostgreSQL instance per assembly, migrating the host, `catalog`, `membership`, **and** `lending` schemas into one template database (the first test fixture composing all three business modules), then cloned per test class
- [X] T048 Create `LendingApplicationTestModule` in `test/ToolShare.Lending.Application.Tests/LendingApplicationTestModule.cs` depending on `LendingApplicationModule`, `LendingEntityFrameworkCoreModule`, `CatalogApplicationModule`, `CatalogEntityFrameworkCoreModule`, `MembershipApplicationModule`, `MembershipEntityFrameworkCoreModule`, `ToolShareEntityFrameworkCoreModule` — modelled on `CatalogApplicationTestModule` (which already composes Catalog + Membership + host) and, like it, **must not** call `AddAlwaysAllowAuthorization()`
- [X] T049 Create `LendingApplicationTestBase`, `LendingAuthorizationTestBase`, `LendingTestDataSeedContributor`, `LendingTestPrincipals`, and `LendingAuthorizationSeedModule` in `test/ToolShare.Lending.Application.Tests/` — seeding member records (via Membership) and a category/tool/instance fixture (via Catalog) for Lending's own synthetic test principals, mirroring the pattern `MembershipTestDataSeedContributor`/`CatalogTestDataSeedContributor` both established
- [X] T050 Verify `dotnet build ToolShare.slnx` succeeds and the DbMigrator applies the new schema against a clean database: `cd src/ToolShare.DbMigrator && dotnet run`

**Checkpoint**: Domain model, schema, the Catalog extension, and test harness ready — user story work
can begin

---

## Phase 3: User Story 1 - Reserve a tool, or join the waitlist (Priority: P1) 🎯 MVP

**Goal**: A member reserves an available instance for a date range within the maximum loan term, or is
offered a FIFO waitlist spot when it is unavailable; they may cancel before checkout; the reservation
respects the member's effective concurrent-loan limit and overdue status as reported by Membership.

**Independent Test**: Reserve a free instance for a date range; attempt to reserve the same instance for
overlapping dates as a second member and confirm a waitlist join is offered; cancel the first
reservation and confirm the instance frees and the waitlisted member receives a time-boxed offer.

### Tests for User Story 1 — write first, must fail

- [X] T051 [P] [US1] Write `ReserveInstanceTests` in `test/ToolShare.Lending.Application.Tests/Reservations/ReserveInstanceTests.cs` covering FR-001: success case (Active status, correct dates), `RES-01` term-exceeded rejection, `RES-02` unavailable-instance rejection
- [X] T052 [P] [US1] Write `OverlapAndWaitlistTests` in `test/ToolShare.Lending.Application.Tests/Reservations/OverlapAndWaitlistTests.cs` covering FR-003/`RES-03`: an overlapping request is refused and offered a waitlist join instead of a dead-end error
- [X] T053 [P] [US1] Write `ConcurrentLoanLimitTests` in `test/ToolShare.Lending.Application.Tests/Reservations/ConcurrentLoanLimitTests.cs` covering `RES-05`, including the reduced limit under a low rating (reads Membership's live standing)
- [X] T054 [P] [US1] Write `OverdueBlockTests` in `test/ToolShare.Lending.Application.Tests/Reservations/OverdueBlockTests.cs` covering `RES-04` — a member with an open, `IsOverdue` loan cannot reserve (manipulate `Loan.IsOverdue` directly via the repository in the test; the worker that sets it live is US5)
- [X] T055 [P] [US1] Write `CancelReservationTests` in `test/ToolShare.Lending.Application.Tests/Reservations/CancelReservationTests.cs` covering `RES-06`: cancellable only while `Active`, rejected once `CheckedOut`
- [X] T056 [P] [US1] Write `WaitlistFifoAndOfferTests` in `test/ToolShare.Lending.Application.Tests/Reservations/WaitlistFifoAndOfferTests.cs` covering `WL-01` (join order), `WL-02` (no duplicate active entry), `WL-03` (cancelling a reservation offers the earliest waiting member)
- [X] T057 [P] [US1] Write `WaitlistOfferExpiryWorkerTests` in `test/ToolShare.Lending.Application.Tests/Reservations/WaitlistOfferExpiryWorkerTests.cs` driving the worker's `DoWorkAsync` directly (not waiting on the real timer): expires an offer past its window and rolls to the next waiting member; leaves a not-yet-expired offer untouched
- [X] T058 [P] [US1] Write `ReservationAuthorizationTests` in `test/ToolShare.Lending.Application.Tests/Authorization/ReservationAuthorizationTests.cs` covering FR-029: creating/cancelling one's own reservation requires only the enrolment gate (no `Lending.*` permission), denied for a deactivated/non-member
- [X] T059 [P] [US1] Write `ConcurrentReservationOverlapTests` in `test/ToolShare.Lending.Application.Tests/Reservations/ConcurrentReservationOverlapTests.cs` covering FR-009: genuinely concurrent (`Task.WhenAll`) reservation attempts for the same instance and overlapping dates — exactly one succeeds, the exclusion constraint (research R3) is the authority, the other is refused and can join the waitlist

### Implementation for User Story 1

- [X] T060 [P] [US1] Create `IReservationAppService`, `CreateReservationDto`, `JoinWaitlistDto`, `ReservationDto`, `WaitlistEntryDto` in `src/ToolShare.Lending.Application.Contracts/Reservations/` per [contracts/lending-app-services.md](./contracts/lending-app-services.md#ireservationappservice--reservations-and-the-waitlist-us1)
- [X] T061 [P] [US1] Create `IMyLendingAppService` in `src/ToolShare.Lending.Application.Contracts/Loans/IMyLendingAppService.cs` with `GetMyReservationsAsync`/`GetMyWaitlistEntriesAsync` only — `GetMyLoansAsync` is added in US2 once `Loan` has a read surface
- [X] T062 [P] [US1] Create `LendingPermissions` and `LendingPermissionDefinitionProvider` in `src/ToolShare.Lending.Application.Contracts/Permissions/` with the `Lending.Loans` group tree (empty of children for now — `Checkout`/`Return`/`Maintenance.Close` are added in US2/US3) per [contracts/lending-permissions.md](./contracts/lending-permissions.md)
- [X] T063 [US1] Implement `ReservationAppService` in `src/ToolShare.Lending.Application/Reservations/ReservationAppService.cs` — `CreateAsync` reads Membership's `IMemberStandingAppService`/`ICommunityRulesLookupAppService` and Catalog's `IToolInstanceLookupAppService` live, then delegates to `ReservationManager`; `CancelAsync` is self-only (`CurrentUser.Id`); `JoinWaitlistAsync` delegates to `WaitlistManager`; `GetListForInstanceAsync` is read-only
- [X] T064 [US1] Implement `MyLendingAppService` in `src/ToolShare.Lending.Application/Loans/MyLendingAppService.cs` (`GetMyReservationsAsync`/`GetMyWaitlistEntriesAsync` only at this phase)
- [X] T065 [US1] Implement `WaitlistOfferExpiryWorker` (`AsyncPeriodicBackgroundWorkerBase`) in `src/ToolShare.Lending.Application/Reservations/WaitlistOfferExpiryWorker.cs` and register it via `IBackgroundWorkerManager.AddAsync` in `LendingApplicationModule.OnApplicationInitializationAsync`
- [X] T066 [P] [US1] Create the reserve/join-waitlist Blazor UI in `src/ToolShare.Lending.Blazor/Pages/Lending/ReserveInstanceModal.razor` (invoked from the catalog instance detail page) and `src/ToolShare.Lending.Blazor/Pages/Lending/MyReservations.razor`
- [X] T067 [P] [US1] Create `LendingMenuContributor` + `LendingMenuNames` in `src/ToolShare.Lending.Blazor/Menus/` and register them in `LendingBlazorModule`
- [X] T068 [US1] Run the **full** suite `dotnet test ToolShare.slnx` and confirm the Lending, Catalog, and Membership suites are all green

**Checkpoint**: Reservations and the waitlist work end-to-end. This is the MVP.

---

## Phase 4: User Story 2 - Check out and return a tool, recording its condition (Priority: P1)

**Goal**: A Librarian checks out an instance against a reservation, marking it on loan in Catalog; a
later return records the instance's condition, closes the loan, and — if the condition worsened — opens
a maintenance request and cascades cancellation to any future reservations for that same instance.

**Independent Test**: Check out an instance against an existing reservation and confirm it shows as on
loan; return it in the same condition and confirm the loan closes and the instance becomes available;
separately, check out and return another instance in a worse condition and confirm the loan still
closes but the instance stays unavailable, referencing an open maintenance request.

### Tests for User Story 2 — write first, must fail

- [X] T069 [P] [US2] Write `CheckOutTests` in `test/ToolShare.Lending.Application.Tests/Loans/CheckOutTests.cs` covering FR-010/`LOAN-01`/`LOAN-02`: success marks the instance `OnLoan` via the Catalog extension, rejected without a matching `Active` reservation, rejected if the instance is already unavailable (`LOAN-06`)
- [X] T070 [P] [US2] Write `ReturnCleanTests` in `test/ToolShare.Lending.Application.Tests/Loans/ReturnCleanTests.cs` covering FR-012: closes the loan, calls `MarkReturnedAsync`, frees the instance, offers the waitlist if one exists
- [X] T071 [P] [US2] Write `ReturnWorsenedTests` in `test/ToolShare.Lending.Application.Tests/Loans/ReturnWorsenedTests.cs` covering FR-012/FR-014/`LOAN-04`: closes the loan, opens exactly one `MaintenanceRequest`, calls `MarkReturnedForMaintenanceAsync` (not `MarkReturnedAsync`), instance stays unavailable
- [X] T072 [P] [US2] Write `EarlyReturnTests` in `test/ToolShare.Lending.Application.Tests/Loans/EarlyReturnTests.cs` covering FR-011's early-return acceptance
- [X] T073 [P] [US2] Write `ReservationCancellationCascadeTests` in `test/ToolShare.Lending.Application.Tests/Reservations/ReservationCancellationCascadeTests.cs` covering `RES-08`/`MAINT-03`: a worsened return cancels every future `Active` reservation for the same instance, leaving the already-`CheckedOut` one that triggered it untouched
- [X] T074 [P] [US2] Write `LoanAuthorizationTests` in `test/ToolShare.Lending.Application.Tests/Authorization/LoanAuthorizationTests.cs` covering FR-029: checkout/return require `Lending.Loans.Checkout`/`.Return`, denied for a plain `Member`
- [X] T075 [P] [US2] Write `MyLoansTests` in `test/ToolShare.Lending.Application.Tests/Loans/MyLoansTests.cs` covering FR-030: a member sees only their own loans via `IMyLendingAppService.GetMyLoansAsync`

### Implementation for User Story 2

- [X] T076 [P] [US2] Add `GetMyLoansAsync` to `IMyLendingAppService` in `src/ToolShare.Lending.Application.Contracts/Loans/IMyLendingAppService.cs` and implement it in `src/ToolShare.Lending.Application/Loans/MyLendingAppService.cs`
- [X] T077 [P] [US2] Create `ILoanAppService`, `CheckOutReservationDto`, `RecordReturnDto`, `LoanDto`, `GetLoanListInput` in `src/ToolShare.Lending.Application.Contracts/Loans/` per [contracts/lending-app-services.md](./contracts/lending-app-services.md#iloanappservice--checkout-return-and-the-roster-view-us2-us4)
- [X] T078 [US2] Add `Lending.Loans.Checkout` and `Lending.Loans.Return` to `LendingPermissions`/`LendingPermissionDefinitionProvider`
- [X] T079 [US2] Implement `LoanAppService.CheckOutAsync` in `src/ToolShare.Lending.Application/Loans/LoanAppService.cs` — reads `ConditionAtCheckout` from Catalog's lookup, creates the `Loan` via `LoanManager` (realizing the reservation in the same operation), calls Catalog's `MarkOnLoanAsync`
- [X] T080 [US2] Implement `LoanAppService.ReturnAsync` — closes the loan via `LoanManager`/`Loan.Return`, opens a `MaintenanceRequest` and calls `MarkReturnedForMaintenanceAsync` on a worsened return (else `MarkReturnedAsync`), cascades reservation cancellation (`ReservationManager.CancelForMaintenanceAsync`) on a worsened return, triggers `WaitlistManager.OfferNextAsync` on a clean return — **no Membership reliability reporting yet** (deferred to US4, T102)
- [X] T081 [US2] Implement `LoanAppService.GetListAsync`/`GetAsync`
- [X] T082 [US2] Grant `Lending.Loans`, `Lending.Loans.Checkout`, `Lending.Loans.Return` to `Librarian` in `src/ToolShare.Application/Identity/LibrarianRoleDataSeedContributor.cs` (cumulative to `Administrator`)
- [X] T083 [P] [US2] Create the checkout/return Blazor UI in `src/ToolShare.Lending.Blazor/Pages/Lending/Loans.razor` (Librarian action against a reservation) and a return dialog with a condition selector
- [X] T084 [US2] Run the **full** suite `dotnet test ToolShare.slnx` and confirm US1 and US2 are both green

**Checkpoint**: The full loan lifecycle (checkout, return, condition, maintenance-opening) works —
reliability reporting to Membership lands in US4.

---

## Phase 5: User Story 3 - Close a maintenance request and restore the instance (Priority: P2)

**Goal**: A Librarian closes an open maintenance request with a stated cost, restoring the instance to
availability and, if a waitlist exists, offering it to the earliest waiting member.

**Independent Test**: With an instance held unavailable by an open maintenance request, close the
request with a stated cost and confirm the instance becomes reservable again and the request's cost is
retained in its history.

### Tests for User Story 3 — write first, must fail

- [X] T085 [P] [US3] Write `CloseMaintenanceRequestTests` in `test/ToolShare.Lending.Application.Tests/Maintenance/CloseMaintenanceRequestTests.cs` covering FR-016/`MAINT-02`: success restores availability via `MarkMaintenanceClosedAsync`, cost is retained and visible afterward (FR-017)
- [X] T086 [P] [US3] Write `MaintenanceCostRequiredTests` in `test/ToolShare.Lending.Application.Tests/Maintenance/MaintenanceCostRequiredTests.cs` covering `MAINT-02`: rejected without a cost, zero is accepted as distinct from omitted
- [X] T087 [P] [US3] Write `OneOpenRequestPerInstanceTests` in `test/ToolShare.Lending.Application.Tests/Maintenance/OneOpenRequestPerInstanceTests.cs` covering `MAINT-01` — the filtered unique index as the authority under concurrency
- [X] T088 [P] [US3] Write `MaintenanceClosingTriggersWaitlistTests` in `test/ToolShare.Lending.Application.Tests/Maintenance/MaintenanceClosingTriggersWaitlistTests.cs` covering `WL-03`'s trigger from a closed maintenance request
- [X] T089 [P] [US3] Write `MaintenanceAuthorizationTests` in `test/ToolShare.Lending.Application.Tests/Authorization/MaintenanceAuthorizationTests.cs` covering FR-029: closing requires `Lending.Maintenance.Close`, denied for a plain `Member`

### Implementation for User Story 3

- [X] T090 [P] [US3] Create `IMaintenanceRequestAppService`, `CloseMaintenanceRequestDto`, `MaintenanceRequestDto` in `src/ToolShare.Lending.Application.Contracts/Maintenance/` per [contracts/lending-app-services.md](./contracts/lending-app-services.md#imaintenancerequestappservice--closing-a-request-us3)
- [X] T091 [US3] Add `Lending.Maintenance.Close` to `LendingPermissions`/`LendingPermissionDefinitionProvider`
- [X] T092 [US3] Implement `MaintenanceRequestAppService.CloseAsync`/`GetOpenListAsync` in `src/ToolShare.Lending.Application/Maintenance/MaintenanceRequestAppService.cs`, triggering `WaitlistManager.OfferNextAsync` after closing
- [X] T093 [US3] Grant `Lending.Maintenance.Close` to `Librarian` in `src/ToolShare.Application/Identity/LibrarianRoleDataSeedContributor.cs`
- [X] T094 [P] [US3] Create the maintenance queue Blazor page in `src/ToolShare.Lending.Blazor/Pages/Lending/MaintenanceRequests.razor`
- [X] T095 [US3] Run the **full** suite `dotnet test ToolShare.slnx` and confirm US1–US3 are all green

**Checkpoint**: The maintenance loop closes — an instance a worsened return took out of circulation can
come back.

---

## Phase 6: User Story 4 - Reliability outcomes flow back to Membership (Priority: P2)

**Goal**: A closed loan reports the applicable reliability outcome(s) to Membership using only its
published contract, using `Loan.Id` as the occurrence identifier; a member's eligibility (concurrent
limit, overdue block) is decided from Membership's live standing, never from a Lending-local guess.

**Independent Test**: Close a loan with a worsened-condition return and confirm the member's reliability
rating (visible on their Membership profile) decreased by the configured damage penalty; separately,
close an on-time, undamaged loan and confirm the rating increases by the configured clean-return
reward; confirm reporting the same loan's outcome twice does not double the effect.

### Tests for User Story 4 — write first, must fail

- [X] T096 [P] [US4] Write `OverdueOutcomeReportedTests` in `test/ToolShare.Lending.Application.Tests/Loans/OverdueOutcomeReportedTests.cs` covering FR-020: a late return reports `OverdueReturn` with `Loan.Id` as `OccurrenceId`, and the member's rating drops by the configured overdue penalty
- [X] T097 [P] [US4] Write `DamageOutcomeReportedTests` in `test/ToolShare.Lending.Application.Tests/Loans/DamageOutcomeReportedTests.cs` covering FR-020: a worsened return reports `DamagedReturn`
- [X] T098 [P] [US4] Write `BothOutcomesReportedTests` in `test/ToolShare.Lending.Application.Tests/Loans/BothOutcomesReportedTests.cs` covering FR-020's "late **and** damaged" edge case: both outcomes are reported against the same `Loan.Id`, relying on Membership's composite `(OccurrenceId, OutcomeType)` idempotency key
- [X] T099 [P] [US4] Write `CleanOutcomeReportedTests` in `test/ToolShare.Lending.Application.Tests/Loans/CleanOutcomeReportedTests.cs` covering FR-020: an on-time, undamaged return reports `CleanReturn`
- [X] T100 [P] [US4] Write `ReliabilityReportIdempotenceTests` in `test/ToolShare.Lending.Application.Tests/Loans/ReliabilityReportIdempotenceTests.cs` covering `LOAN-05`: `Loan.ReliabilityReportedAt` prevents a retried close from reporting a second time
- [X] T101 [P] [US4] Write `EligibilityConsumesMembershipTests` in `test/ToolShare.Lending.Application.Tests/Reservations/EligibilityConsumesMembershipTests.cs` covering FR-018/spec.md US1 scenarios 4–5: changing a member's rating (via Membership's own admin surface) measurably changes their effective concurrent-loan limit as `ReservationAppService.CreateAsync` sees it, proving the check is live, not cached or locally computed
- [X] T102 [P] [US4] Write `NonMemberRefusedTests` in `test/ToolShare.Lending.Application.Tests/Authorization/NonMemberRefusedTests.cs` covering FR-019: Membership reporting a non-enrolled identity causes reservation/checkout to be refused

### Implementation for User Story 4

- [X] T103 [US4] Extend `LoanAppService.ReturnAsync` in `src/ToolShare.Lending.Application/Loans/LoanAppService.cs` (T080) to call Membership's `IReliabilityReportingAppService` with the applicable outcome(s) — overdue if `IsOverdue`, damage if worsened, clean-return only if neither — supplying `Loan.Id` as `OccurrenceId`, and set `Loan.ReliabilityReportedAt` once all applicable reports succeed (`LOAN-05`, research R8 — no local retry wrapper)
- [X] T104 [US4] Run the **full** suite `dotnet test ToolShare.slnx` and confirm US1–US4 are all green

**Checkpoint**: The rating mechanism the whole product depends on is alive end-to-end.

---

## Phase 7: User Story 5 - Reminders and overdue tracking (Priority: P3)

**Goal**: A return reminder is generated ahead of the planned return date; a loan not returned by then
is marked overdue and a notice is generated; a Librarian can distinguish overdue loans in the roster
view.

**Independent Test**: Create a loan with a return date in the near future and confirm a reminder is
generated ahead of it by the configured lead time; let (or simulate) its return date pass without a
return and confirm it is marked overdue and blocks new reservations for that member.

### Tests for User Story 5 — write first, must fail

- [X] T105 [P] [US5] Write `ReturnReminderWorkerTests` in `test/ToolShare.Lending.Application.Tests/Loans/ReturnReminderWorkerTests.cs` covering FR-022: generates `LendingNotificationDueEto` (`ReturnReminder`) within `rules.ReminderLeadTimeDays`, idempotent via `Loan.ReminderSentAt`
- [X] T106 [P] [US5] Write `OverdueMarkingWorkerTests` in `test/ToolShare.Lending.Application.Tests/Loans/OverdueMarkingWorkerTests.cs` covering FR-023/FR-025: marks `IsOverdue`, generates `LendingNotificationDueEto` (`Overdue`), idempotent via `Loan.OverdueNoticeSentAt`, and the flag survives an eventual return
- [X] T107 [P] [US5] Write `OverdueListFilterTests` in `test/ToolShare.Lending.Application.Tests/Loans/OverdueListFilterTests.cs` covering FR-024: `GetLoanListInput.OnlyOverdue` distinguishes overdue loans in `ILoanAppService.GetListAsync`
- [X] T108 [P] [US5] Write `OverdueBlocksNewReservationLiveTests` in `test/ToolShare.Lending.Application.Tests/Reservations/OverdueBlocksNewReservationLiveTests.cs` closing the loop opened by T054/T101: an open loan the `OverdueMarkingWorker` actually marks overdue (not manually flagged by the test) blocks that member's next reservation attempt

### Implementation for User Story 5

- [X] T109 [P] [US5] Add `OnlyOverdue` to `GetLoanListInput` in `src/ToolShare.Lending.Application.Contracts/Loans/GetLoanListInput.cs` and the corresponding filter to `LoanAppService.GetListAsync` in `src/ToolShare.Lending.Application/Loans/LoanAppService.cs`
- [X] T110 [US5] Implement `ReturnReminderWorker` (`AsyncPeriodicBackgroundWorkerBase`) in `src/ToolShare.Lending.Application/Loans/ReturnReminderWorker.cs` and register it in `LendingApplicationModule.OnApplicationInitializationAsync`
- [X] T111 [US5] Implement `OverdueMarkingWorker` in `src/ToolShare.Lending.Application/Loans/OverdueMarkingWorker.cs` and register it alongside T110
- [X] T112 [P] [US5] Add an overdue indicator/filter to the loan list Blazor page (`Loans.razor`, T083)
- [X] T113 [US5] Run the **full** suite `dotnet test ToolShare.slnx` and confirm all five user stories are green

**Checkpoint**: All five user stories are independently functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T114 [P] Complete the localization sweep in `src/ToolShare.Lending.Domain.Shared/Localization/Lending/en.json` — every error code, permission name, menu name, and any UI label actually wired to `L[...]` has an entry, matching the scope Catalog and Membership both settled on (no raw keys rendered)
- [X] T115 [P] Write `LendingModuleBoundaryTests` in `test/ToolShare.Lending.Application.Tests/LendingModuleBoundaryTests.cs` scanning `src/ToolShare.Lending.Application/**/*.cs` and `src/ToolShare.Lending.Domain/**/*.cs` for forbidden `ToolShare.Catalog.Domain`/`…EntityFrameworkCore` and `ToolShare.Membership.Domain`/`…EntityFrameworkCore` imports — the reverse-direction check 002/003 never needed, since Lending is the first module to be a heavy *consumer* of two others' contracts rather than only a producer
- [X] T116 [P] Update `CLAUDE.md` to list `ToolShare.Lending.*` as the third feature module, note the `lending` schema and the `btree_gist` extension, and record that Catalog's `ToolInstanceCirculationState` now includes `OnLoan`/`UnderMaintenance`
- [X] T117 [P] Add a `LendingPermissionDefinitionProvider` localization check to `test/ToolShare.Lending.Application.Tests/` asserting every defined permission has a display name (mirrors Membership's equivalent test)
- [X] T118 Verify the performance goals from [plan.md](./plan.md): measure the reservation-eligibility check's warm p95 (< 150 ms) and confirm a full background-worker sweep completes in under 5 seconds at the documented scale
- [X] T119 Execute every scenario in [quickstart.md](./quickstart.md) end-to-end against a clean checkout, including verifying the exclusion constraint exists (`\d lending."Reservations"`) and the manual boundary `grep`
- [X] T120 Run `dotnet build ToolShare.slnx` and `dotnet test ToolShare.slnx` and confirm 0 errors and a fully green suite across every test project

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **User Stories (Phases 3–7)**: All depend on Foundational
- **Polish (Phase 8)**: Depends on all desired stories being complete

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational (including the Catalog extension and the three-module test
  harness). Fully independent of US2–US5 except that it reads Membership's contracts, already shipped.
- **US2 (P1)**: Depends on Foundational **and US1** — checkout requires an existing `Active` reservation
  (`RES-07`). Extends the same `LoanAppService` US4 later extends further.
- **US3 (P2)**: Depends on Foundational **and US2** — a `MaintenanceRequest` only ever opens via a
  worsened return (US2's `LOAN-04`); US3 only adds the *closing* half.
- **US4 (P2)**: Depends on Foundational **and US2** — extends the same `ReturnAsync` method US2 created
  (T080 → T103), adding the outbound Membership call. Its eligibility-side test (T101) also exercises
  US1's `RES-04`/`RES-05`, which already consume Membership live from US1 onward.
- **US5 (P3)**: Depends on Foundational **and US2** (needs `Loan` to exist) and, for its closing-the-loop
  test (T108), **US1 and US4** (needs the reservation-eligibility path and the overdue block already
  wired).

> Unlike 003 (where only two cross-story dependencies existed), this feature's five stories form a
> mostly linear chain — US1 → US2 → {US3, US4} → US5 — because a loan cannot exist without a
> reservation, and neither maintenance nor rating reporting can exist without a loan. Only US3 and US4
> are mutually independent of each other (both depend on US2 alone).

### Within Each User Story

- Tests are written **first** and must fail before the implementation they cover
- `Domain.Shared` types → domain entities → repositories → app services → Blazor pages
- Contract interfaces before their implementations
- Story complete and green before moving to the next priority

### Parallel Opportunities

- T007–T008 (test projects) in parallel during Setup
- T011–T017 (all `Domain.Shared` types) fully parallel
- T018–T021 (all four domain unit test files) fully parallel
- T022–T026 mostly parallel (different files; `Reservation`/`WaitlistEntry`/`Loan`/`MaintenanceRequest`
  have no compile-time dependency on each other, only on `Domain.Shared`)
- T031, T034–T035 parallel within the EF phase
- T037–T041, T044 parallel within the Catalog-extension phase (T039 depends on T037; T042 depends on
  T040/T041's DTOs existing)
- All test-writing tasks inside a story phase are parallel (T051–T059, T069–T075, T085–T089,
  T096–T102, T105–T108)
- With multiple developers: after Phase 2, one developer can take US1 while another prepares US2's
  scaffolding in parallel (though US2's tests cannot pass until US1's `Reservation`/checkout path
  exists) — in practice this feature's linear dependency chain makes strict priority-order delivery
  by one team the more natural strategy, unlike 003's more parallel-friendly story graph

---

## Parallel Example: User Story 1

```bash
# Launch all US1 test files together (they must fail first):
Task: "Write ReserveInstanceTests in test/ToolShare.Lending.Application.Tests/Reservations/ReserveInstanceTests.cs"
Task: "Write OverlapAndWaitlistTests in test/ToolShare.Lending.Application.Tests/Reservations/OverlapAndWaitlistTests.cs"
Task: "Write ConcurrentLoanLimitTests in test/ToolShare.Lending.Application.Tests/Reservations/ConcurrentLoanLimitTests.cs"
Task: "Write OverdueBlockTests in test/ToolShare.Lending.Application.Tests/Reservations/OverdueBlockTests.cs"
Task: "Write CancelReservationTests in test/ToolShare.Lending.Application.Tests/Reservations/CancelReservationTests.cs"
Task: "Write WaitlistFifoAndOfferTests in test/ToolShare.Lending.Application.Tests/Reservations/WaitlistFifoAndOfferTests.cs"
Task: "Write WaitlistOfferExpiryWorkerTests in test/ToolShare.Lending.Application.Tests/Reservations/WaitlistOfferExpiryWorkerTests.cs"
Task: "Write ReservationAuthorizationTests in test/ToolShare.Lending.Application.Tests/Authorization/ReservationAuthorizationTests.cs"
Task: "Write ConcurrentReservationOverlapTests in test/ToolShare.Lending.Application.Tests/Reservations/ConcurrentReservationOverlapTests.cs"

# Then launch the independent contract definitions together:
Task: "Create IReservationAppService and DTOs in src/ToolShare.Lending.Application.Contracts/Reservations/"
Task: "Create IMyLendingAppService in src/ToolShare.Lending.Application.Contracts/Loans/"
Task: "Create LendingPermissions in src/ToolShare.Lending.Application.Contracts/Permissions/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL** — blocks everything, and includes the Catalog extension
   even though US1 itself doesn't call it, since US2 needs it immediately after and re-doing Foundational
   twice would be wasted motion)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: reserve, get offered a waitlist on conflict, cancel, confirm the next waitlisted
   member is offered the slot
5. At this point members can commit to borrowing something, even though nothing can yet be physically
   checked out — a real but partial increment

### Incremental Delivery

1. Setup + Foundational → domain model, schema, and the Catalog extension ready
2. US1 → reservations + waitlist → a partial MVP (commitment without physical handover)
3. US2 → checkout + return + condition recording → **the first fully closed loan lifecycle** (the
   more meaningful MVP milestone, since this is when a tool actually leaves and returns)
4. US3 → maintenance requests close → the fleet can recover from damage
5. US4 → reliability outcomes reach Membership → the product's core self-regulation mechanism goes live
6. US5 → reminders and overdue tracking → reduces reliance on a Librarian noticing manually

### Risk Notes

- **T039/T042/T043 are the highest-risk tasks in the feature.** They modify an already-shipped module's
  production code. Additive-only by design (research R2/R10) — if any pre-existing Catalog test needs
  to change to accommodate them, that is a signal the "additive only" premise has been violated and the
  approach needs re-examination before proceeding.
- **T036's hand-edited migration is easy to get wrong.** EF Core's migration scaffolding has no
  first-class API for `EXCLUDE USING gist`; the raw SQL must be added to the generated migration file by
  hand, in both the `Up` and `Down` methods, and `btree_gist` must be created before the constraint that
  depends on it.
- **T103 (the Membership reporting call) is easy to duplicate accidentally.** It must be added to the
  *same* `ReturnAsync` method T080 already created, not a second code path — re-read `LOAN-05` and
  research R8 before touching this task.
- **US5 is the first genuinely optional-feeling story** (P3, "the feature is fully usable without it" per
  spec.md's own reasoning) — if scope must be cut, cut US5 before cutting US3 or US4, which close loops
  US1/US2 leave open.

---

## Notes

- [P] tasks touch different files and have no incomplete dependencies
- [Story] labels map each task to a spec user story for traceability
- Every test task must be observed failing before its implementation task begins (Principle V)
- Commit after each task or logical group; stop at any checkpoint to validate a story independently
- No Membership **production** file is modified by this feature — only its published contracts are
  consumed, exactly as they shipped in 003

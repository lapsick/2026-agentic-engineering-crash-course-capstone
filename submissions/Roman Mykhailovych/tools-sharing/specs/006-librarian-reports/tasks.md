---

description: "Task list for 006-librarian-reports"
---

# Tasks: Librarian Reports

**Input**: Design documents from `/specs/006-librarian-reports/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: MANDATORY. Per **Constitution Principle V (Test-First Discipline, NON-NEGOTIABLE)**, every
story below writes its tests first and they must fail before the implementation task runs. Domain rules
get xUnit unit tests with no database; application behavior gets integration tests against real
PostgreSQL via the existing Testcontainers fixture.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are given in every task

## Path Conventions

This feature adds **no new project**. All paths are existing projects under `src/` and `test/`, per
[plan.md](./plan.md)'s Structure Decision — a `Reports/` feature slice inside the shipped Lending
module.

---

## Phase 1: Setup

**Purpose**: Establish a known-good baseline. There is no scaffolding to do — this feature adds no
project, package, schema, or migration ([research.md](./research.md) R1, R7), so Phase 1 exists only to
make any later red test attributable to this feature's changes.

- [X] T001 Run `dotnet build ToolShare.slnx` and `dotnet test ToolShare.slnx` from the repo root and confirm both are fully green before making any change; record the passing test count so 002–005 regressions are detectable later

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The permission boundary, the shared error code, and the empty service/page shells that all
three reports hang off. Every user story depends on this phase.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Add the `Reports` nested class with `Default = GroupName + ".Reports"` to `src/ToolShare.Lending.Application.Contracts/Permissions/LendingPermissions.cs`, following the existing `Loans`/`Maintenance` nested-class shape
- [X] T003 [P] Add `InvalidReportDateRange` constant to `src/ToolShare.Lending.Domain.Shared/LendingDomainErrorCodes.cs`, matching the existing `Lending:`-prefixed error-code convention
- [X] T004 Register the permission via `lendingGroup.AddPermission(LendingPermissions.Reports.Default, L("Permission:Lending.Reports"))` in `src/ToolShare.Lending.Application.Contracts/Permissions/LendingPermissionDefinitionProvider.cs` as a top-level permission in the `Lending` group, not a child of `Lending.Loans` (depends on T002; rationale in [research.md](./research.md) R8)
- [X] T005 Add three localization keys to `src/ToolShare.Lending.Domain.Shared/Localization/Lending/en.json`: `"Lending:InvalidReportDateRange"` (range-rejection message), `"Permission:Lending.Reports": "View reports"`, and `"Menu:Lending.Reports": "Reports"` (depends on T002, T003; the shipped `LendingPermissionLocalizationTests` fails without the permission key)
- [X] T006 Append `LendingPermissions.Reports.Default` to the `LibrarianLendingPermissions` array in `src/ToolShare.Application/Identity/LibrarianRoleDataSeedContributor.cs` — this single edit also grants Administrator, because `MembershipRoleDataSeedContributor` composes its grants from that array (depends on T002)
- [X] T007 Create `src/ToolShare.Lending.Application.Contracts/Reports/IReportAppService.cs` declaring all three methods and co-locating `ToolPopularityReportInput`, `MaintenanceCostReportInput`, `OverdueLoanReportItemDto`, `ToolPopularityReportItemDto`, and `MaintenanceCostReportDto`, exactly as specified in [contracts/lending-reports-app-service.md](./contracts/lending-reports-app-service.md) (co-location matches the existing `IMaintenanceRequestAppService.cs` shape)
- [X] T008 Create `src/ToolShare.Lending.Application/Reports/ReportAppService.cs` as a shell: class-level `[Authorize(LendingPermissions.Reports.Default)]`, constructor injection of `IRepository<Loan, Guid>`, `IRepository<MaintenanceRequest, Guid>`, `IToolInstanceLookupAppService`, and `IMemberStandingAppService`, a shared private date-range guard that (in order) rejects a null bound where the report requires one and rejects `From > To`, both with `BusinessException(LendingDomainErrorCodes.InvalidReportDateRange)`, and all three methods throwing `NotImplementedException` until their story lands (depends on T002, T003, T007; use the default repository, **not** `ILoanRepository` — see [research.md](./research.md) R3)
- [X] T009 Create `src/ToolShare.Lending.Blazor/Pages/Lending/Reports.razor` as a shell: `@page "/lending/reports"`, `@attribute [Authorize(LendingPermissions.Reports.Default)]`, `@inherits LendingComponentBase`, `@inject IReportAppService`, and an **empty** `MudTabs` container — each story adds its own `MudTabPanel` in its own UI task, so an MVP shipping only US1 shows one tab rather than one tab plus two permanently blank ones (depends on T002, T007)
- [X] T010 Add a `Reports` entry to `src/ToolShare.Lending.Blazor/Menus/LendingMenuNames.cs` and a menu item pointing at `/lending/reports` with `requiredPermissionName: LendingPermissions.Reports.Default` in `src/ToolShare.Lending.Blazor/Menus/LendingMenuContributor.cs` (depends on T002, T009)
- [X] T011 Run `dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj --filter "FullyQualifiedName~LendingPermissionLocalizationTests"` and confirm the shipped test now covers and passes for the new permission (depends on T004, T005)

**Checkpoint**: The permission exists, is granted, and is localized; the page and service shells compile.
No report returns data yet.

---

## Phase 3: User Story 1 — Overdue Loans at a Glance (Priority: P1) 🎯 MVP

**Goal**: The librarian opens a report and sees every currently-overdue loan — member, tool/instance,
checkout date, planned return date, days overdue — most overdue first.

**Independent Test**: Check out an instance against a reservation whose range already ended, then open
the overdue report **without waiting for any background worker**; the loan appears with all its fields.
Record a return and confirm it disappears. With nothing overdue, confirm an explicit empty state.

### Tests for User Story 1 (write first, must fail) ⚠️

- [X] T012 [P] [US1] Create `test/ToolShare.Lending.Domain.Tests/Loans/LoanOverdueCalculationTests.cs` — pure xUnit, **no database**, following the existing `LoanLifecycleTests` construction pattern: assert a loan is *not* overdue on its planned return date, *is* overdue the following day with `DaysOverdueAsOf == 1`, is never overdue once `ReturnedAt` is set however late the return was, and returns `0` days when not overdue
- [X] T013 [P] [US1] Create `test/ToolShare.Lending.Application.Tests/Reports/OverdueReportTests.cs` covering: an overdue loan appears **without running `OverdueMarkingWorker`** (guarantee B2 — this is the test that would catch a flag-based implementation), a returned loan is absent (B3), results are ordered most-overdue-first (B4), an empty result is a successful empty list (B5), member and tool names are resolved, an unresolvable **member** leaves `MemberDisplayName` null and an unresolvable **tool instance** leaves `ToolName` null, in both cases without dropping the row (B9), and — for FR-011/B1 — that calling the report mutates nothing: snapshot the loan row count and the `IsOverdue`/`ReturnedAt` values of a known loan before and after the call and assert they are unchanged
- [X] T014 [P] [US1] Create `test/ToolShare.Lending.Application.Tests/Reports/ReportDriftTests.cs` asserting that the set of loan ids returned by `GetOverdueLoansAsync` equals the set produced by loading every loan and filtering in memory through `Loan.IsOverdueAsOf(today)` — the structural guard that keeps the SQL predicate and the domain method from diverging ([research.md](./research.md) R2)
- [X] T015 [P] [US1] Create `test/ToolShare.Lending.Application.Tests/Authorization/ReportAuthorizationTests.cs` asserting a principal holding neither Librarian nor Administrator is refused with `AbpAuthorizationException`, following the existing `MaintenanceAuthorizationTests` pattern — this exercises the class-level guard and therefore covers all three methods (FR-012, SC-005)

### Implementation for User Story 1

- [X] T016 [US1] Add the pure methods `bool IsOverdueAsOf(DateOnly asOf)` and `int DaysOverdueAsOf(DateOnly asOf)` to `src/ToolShare.Lending.Domain/Loans/Loan.cs`, alongside the existing `IsWorsened()`; add no field, no constructor change, and no state transition (rules in [data-model.md](./data-model.md) §2) — makes T012 pass
- [X] T017 [US1] Implement `GetOverdueLoansAsync` in `src/ToolShare.Lending.Application/Reports/ReportAppService.cs`: filter in SQL on `ReturnedAt == null && PlannedReturnDate < today` where `today = DateOnly.FromDateTime(Clock.Now)`, execute via `AsyncExecuter`, batch-resolve names through `IMemberStandingAppService.GetByIdsAsync` and `IToolInstanceLookupAppService.GetByIdsAsync` (one call each, never per row), compute `DaysOverdue` via `Loan.DaysOverdueAsOf`, and order most-overdue-first. Do **not** read `Loan.IsOverdue` (depends on T016; makes T013, T014, T015 pass)
- [X] T018 [US1] Add the overdue `MudTabPanel` to the `MudTabs` container in `src/ToolShare.Lending.Blazor/Pages/Lending/Reports.razor`: a `MudTable` over `OverdueLoanReportItemDto` with member, tool, serial, checkout date, planned return date and days-overdue columns, a `NoRecordsContent` empty state, and raw ids rendered where a resolved name is null (depends on T009, T017)

**Checkpoint**: User Story 1 is fully functional and independently demonstrable. This is a shippable MVP
— the most operationally urgent of the three reports works end to end.

---

## Phase 4: User Story 2 — Most-Borrowed Tools (Priority: P2)

**Goal**: The librarian sees tools ranked by number of loans, optionally narrowed to a date range.

**Independent Test**: Borrow tool A three times and tool B once across different dates; open the report
with no range and confirm A ranks above B with counts 3 and 1; set a narrower range and confirm the
counts and ranking change accordingly.

### Tests for User Story 2 (write first, must fail) ⚠️

- [X] T019 [P] [US2] Create `test/ToolShare.Lending.Application.Tests/Reports/PopularityReportTests.cs` covering: all-time ranking ordered by loan count descending (FR-001), a date range restricting the count to loans checked out within it (FR-002), loans of a **retired** instance still counting (FR-003/B6 — retire an instance after borrowing it and assert the count is unchanged), several instances of one tool folding into a single ranked row ([research.md](./research.md) R4), and a never-borrowed tool being absent rather than breaking the report (R5), a range containing **no loans at all** returning a successful empty list rather than an error (FR-013/B5), and — for FR-011/B1 — that the call mutates nothing (snapshot the loan row count before and after and assert it is unchanged)
- [X] T020 [P] [US2] Create `test/ToolShare.Lending.Application.Tests/Reports/PopularityRangeValidationTests.cs` asserting that `GetToolPopularityAsync` with `From > To` throws `BusinessException` carrying `LendingDomainErrorCodes.InvalidReportDateRange` rather than returning an empty list (FR-009/B8), that both bounds are inclusive (a loan checked out exactly on `From` and one exactly on `To` both count), that supplying only one bound is accepted as open-ended on the other side, and that omitting both yields the all-time result

### Implementation for User Story 2

- [X] T021 [US2] Implement `GetToolPopularityAsync` in `src/ToolShare.Lending.Application/Reports/ReportAppService.cs`: apply the shared date-range guard, filter `Loan` by the optional `CheckedOutAt` range (inclusive, using `< To.AddDays(1)`), `GroupBy(l => l.ToolInstanceId)` with `Count()` **in SQL**, batch-resolve the resulting instance ids via `IToolInstanceLookupAppService.GetByIdsAsync`, then fold by `ToolId` in memory summing counts and order descending (depends on T008; makes T019, T020 pass)
- [X] T022 [US2] Add the popularity `MudTabPanel` to the `MudTabs` container in `src/ToolShare.Lending.Blazor/Pages/Lending/Reports.razor`: two `MudDatePicker`s bound to an optional range, a `MudTable` of tool name and loan count, and an empty state (depends on T009, T021; edits the same file as T018 — sequence after it)

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 — Maintenance Cost Over a Period (Priority: P3)

**Goal**: The librarian selects a date range and sees the total cost of maintenance closed in it.

**Independent Test**: Close two maintenance requests with costs on different dates and leave a third
open; select a range covering only the first closure and confirm the total equals that one cost, the
count is 1, and the open request contributes nothing.

### Tests for User Story 3 (write first, must fail) ⚠️

- [X] T023 [P] [US3] Create `test/ToolShare.Lending.Application.Tests/Reports/MaintenanceCostReportTests.cs` covering: the total summing only requests **closed** within the range (FR-007), an **open** request contributing nothing (FR-008/B7), a range with no closures returning `TotalCost == 0m` and `ClosedRequestCount == 0` as a success rather than an error (FR-013/B5), two requests for the same instance closed the same day both contributing their own cost (spec Edge Cases), attribution by `ClosedAt` rather than `OpenedAt` (close in a later period than the open and assert which period is charged), and — for FR-011/B1 — that the call mutates nothing (snapshot the maintenance-request row count and a known request's `Status`/`ClosedAt`/`Cost` before and after and assert they are unchanged)
- [X] T024 [P] [US3] Create `test/ToolShare.Lending.Application.Tests/Reports/MaintenanceCostRangeValidationTests.cs` — its **own** file, so US3 stays shippable without US2 — asserting for `GetMaintenanceCostAsync`: `From > To` throws `BusinessException` with `LendingDomainErrorCodes.InvalidReportDateRange` (FR-009/B8), a **null** `From` or `To` throws the same rather than defaulting to `0001-01-01` and returning a near-all-time total (see the nullability note in [contracts/lending-reports-app-service.md](./contracts/lending-reports-app-service.md#inputs)), and both bounds are inclusive (a request closed exactly on `From` and one exactly on `To` both count)

### Implementation for User Story 3

- [X] T025 [US3] Implement `GetMaintenanceCostAsync` in `src/ToolShare.Lending.Application/Reports/ReportAppService.cs`: apply the shared date-range guard, filter `MaintenanceRequest` on `Status == Closed && ClosedAt >= From && ClosedAt < To.AddDays(1)`, `Sum(x => x.Cost)` and `Count()` in SQL, coalescing the nullable sum to `0m` for the empty case, and echo the input range into the result (depends on T008; makes T023, T024 pass — no cross-module call is needed by this report)
- [X] T026 [US3] Add the maintenance-cost `MudTabPanel` to the `MudTabs` container in `src/ToolShare.Lending.Blazor/Pages/Lending/Reports.razor`: two `MudDatePicker`s that must both be set before the report runs, the total, the closed-request count, and a zero-state that reads as a result rather than an error (depends on T009, T025; edits the same file as T018 and T022 — sequence after them)

**Checkpoint**: All three reports are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T027 Run `dotnet test ToolShare.slnx` and confirm every pre-existing 002–005 test still passes against the T001 baseline count — this feature's `Loan` change and seeder edit were designed to be additive, so any pre-existing red test means they were not
- [ ] T028 Walk scenarios V1–V10 in [quickstart.md](./quickstart.md) against a running app (`docker compose up -d`, re-run `DbMigrator` to seed the new permission grant, then `dotnet run` the Blazor host), paying particular attention to **V1** (the report must show a freshly-overdue loan without waiting an hour for `OverdueMarkingWorker`) and **V10** (a plain Member sees no menu item and is refused at the route)
  - **Partially done — the interactive walkthrough is still owed by a human.** Verified automatically:
    `docker compose up -d` + `DbMigrator` completes applying **no** schema change; the new grant lands
    for both roles (`AbpPermissionGrants` holds `Lending.Reports` for `Librarian`, `Administrator` and
    `admin`); the Blazor host starts and `/lending/reports` is registered and refuses an
    unauthenticated request (302 → `/Account/Login`), which is the routing half of **V10**. Still to be
    walked in a browser: **V1–V9**, and V10's "the menu item is absent for a plain Member". Their
    behavior is covered by the integration suite (B1–B10 each have a test, including V1's
    without-the-worker guarantee in `OverdueReportTests` and the drift guard in `ReportDriftTests`),
    but the on-screen rendering has not been eyeballed.
- [X] T029 Run `git status` and confirm there is **no** new EF Core migration, **no** new project, and **no** change to `ToolShare.slnx`; any of the three means the plan's central premise ([research.md](./research.md) R1) was not followed
- [X] T030 [P] Add `Reports/` to the list of Lending feature slices in the "Feature slices within a module" section of `CLAUDE.md`, so the module's shape stays documented for future work

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately
- **Foundational (Phase 2)**: depends on T001 — **blocks all user stories**
- **User Stories (Phases 3–5)**: all depend on Phase 2 completing; then sequential in priority order for
  the file-contention reason below
- **Polish (Phase 6)**: depends on every story that is being shipped

### User Story Dependencies

None of the three stories depends on another's *behavior* — each is independently demonstrable and
could be shipped alone once Phase 2 is done. US1 alone is a complete MVP.

### Within Each User Story

- Tests are written first and MUST fail before the implementation task runs (Constitution Principle V)
- Domain before application before UI (US1: T016 → T017 → T018)
- Story complete and checkpointed before moving to the next priority

---

## Parallel Opportunities

**Honest constraint, deviating from the generic template**: the three stories **cannot** be developed
concurrently by different people without conflict. All three implementations edit
`ReportAppService.cs` (T017, T021, T025) and all three UI tasks edit `Reports.razor` (T018, T022, T026).
That is a direct consequence of the design decision in [research.md](./research.md) R1 — one service and
one page for one librarian capability — and it is the right trade for a feature this size, but it means
the realistic execution is **sequential by priority**: US1 → US2 → US3.

What genuinely parallelizes:

- **Phase 2**: T002 and T003 are independent files. T004/T005/T006 all depend on T002 but touch three
  different files, so they parallelize once T002 lands.
- **Within US1**: T012, T013, T014, T015 are four separate new test files — all four in parallel.
- **Within US2**: T019 and T020 are two separate new test files — both in parallel.
- **Within US3**: T023 and T024 are two separate new test files — both in parallel.
- **Across the boundary**: every story's test-writing tasks (T019, T020, T023, T024) touch only new test
  files and could be written while US1's implementation is in progress, then run red until their
  implementation task lands. Each story owns its own range-validation file, so no story's tests depend
  on another story having shipped.
- **Phase 6**: T030 (a `CLAUDE.md` edit) is independent of everything else.

### Parallel Example: User Story 1 tests

```bash
# All four US1 test files are new and independent — write them together:
Task: "Domain unit tests in test/ToolShare.Lending.Domain.Tests/Loans/LoanOverdueCalculationTests.cs"
Task: "Integration tests in test/ToolShare.Lending.Application.Tests/Reports/OverdueReportTests.cs"
Task: "Drift test in test/ToolShare.Lending.Application.Tests/Reports/ReportDriftTests.cs"
Task: "Authorization test in test/ToolShare.Lending.Application.Tests/Authorization/ReportAuthorizationTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 (T001) — baseline green
2. Phase 2 (T002–T011) — permission, shells, menu
3. Phase 3 (T012–T018) — the overdue report
4. **STOP and VALIDATE**: quickstart V1–V4 and V10
5. Shippable: the most operationally urgent report works, and the other two tabs simply are not there yet

### Incremental Delivery

1. Setup + Foundational → the permission is granted and the page exists
2. US1 → overdue report → demo (**MVP**)
3. US2 → popularity report → demo
4. US3 → maintenance cost report → demo
5. Phase 6 → full-suite regression, quickstart walkthrough, no-migration check

Each story adds one tab and one service method without touching the previous stories' behavior.

---

## Notes

- `[P]` = different files, no dependencies on incomplete tasks
- Verify each story's tests fail before implementing it — that failure is what proves the test is
  testing the new behavior rather than passing vacuously
- **T013 and T014 are the two tests worth not weakening**: T013's "without running the worker" clause and
  T014's set-equality assertion are what enforce [research.md](./research.md) R2's live-computation
  decision. An implementation that reads `Loan.IsOverdue` passes neither
- Commit after each task or logical group
- Expected totals when done: **0** new projects, **0** new packages, **0** new migrations

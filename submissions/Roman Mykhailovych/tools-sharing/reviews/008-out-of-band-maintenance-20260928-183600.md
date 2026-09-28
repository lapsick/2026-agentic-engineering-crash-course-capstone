# Code review — 008-out-of-band-maintenance

- **Date**: 2026-09-28 18:36
- **Base ref**: HEAD (`bbbd230`); branch `roman-mykhailovych` has no upstream, so uncommitted changes only (staged + unstaged + untracked)
- **Scope**: whole diff
- **Feature**: specs/008-out-of-band-maintenance
- **Gate verdict**: PASS (`green-runs/20260928-183109-gate`): Catalog.Domain.Tests 81/81, Lending.Domain.Tests 70/70, Catalog.Application.Tests 80/80, Lending.Application.Tests 139/139, module-boundary audit clean
- **Reviewer**: `toolshare-reviewer` subagent (project agent definition, `.claude/agents/toolshare-reviewer.md`)

---

Reviewed the full diff for 008-out-of-band-maintenance (uncommitted, staged + untracked, against HEAD bbbd230).

Scope covered: spec.md/plan.md/data-model.md/contracts/tasks.md; Catalog `ToolInstance.SendToMaintenance` (IR-09) and its app-service/contract extension; Lending `MaintenanceRequest`/`MaintenanceManager`/`IInstanceLock`/`EfCoreInstanceLock`; `Reservation.CancelUncollectedForMaintenance`/`ReservationManager` (RES-09); `WaitlistEntry.Withdraw`/`WaitlistManager` (WL-07/08); `MaintenanceRequestAppService.ReportAsync`; the instance-lock additions to `ReservationAppService.CreateAsync` and `LoanAppService.CheckOutAsync` (R4 "Amended"); the migration + CHECK constraint + model snapshot; permissions/error codes/localization; Blazor pages (`ReportMaintenance.razor`, `ToolDetail.razor`, `MaintenanceRequests.razor`, `Reports.razor`); and the full new/changed test suite (domain, application, contract, boundary, concurrency, migration-shape) plus the `PostgreSqlContainerFixture` pool-cap change.

Findings

Blocking: none.

Important: none.

Nice-to-have:
- `src/ToolShare.Lending.Application/Maintenance/MaintenanceRequestAppService.cs:~100` — `reporter.MemberId!.Value` uses a null-forgiving operator. In practice the membership gate (`MembershipMethodInvocationAuthorizationService`) guarantees the caller is an enrolled Active member before this code runs, so `MemberId` should never be null here, but if that invariant were ever violated (e.g. gate bypass bug, or a member record without `MemberId` populated) this would throw a raw `NullReferenceException` instead of a `BusinessException`. Not a defect given current guarantees; a defensive `?? throw new BusinessException(...)` would fail more legibly if that invariant ever breaks.

Verification notes (why no blocking/important findings):
- **Constitution II/III**: `IToolInstanceCirculationReportingAppService.MarkSentToMaintenanceAsync` is additive-only (new method, no signature changes to the four existing ones); Lending never references Catalog's `Domain`/`EntityFrameworkCore`, only the contract, confirmed by the new `Catalog_Blazor_does_not_reference_or_import_any_Lending_project` boundary test and the existing module-boundary audit (gate: clean). No cross-schema FK — `ToolInstanceId`/`ReportedByMemberId` are plain `Guid`/`Guid?` with no FK, matching data-model.md.
- **Constitution IV (append-only)**: `MaintenanceRequest.ReportOutOfBand` inserts a new row; `SendToMaintenance` appends one `ToolInstanceStateChange` row (no update to earlier rows); `Reservation.CancelUncollectedForMaintenance` is a one-way `Active → Cancelled` terminal transition; `WaitlistEntry.Withdraw` is a new terminal state (`Withdrawn`) plus a fresh re-queued row rather than resetting the withdrawn entry — verified in `WaitlistManager.WithdrawOutstandingOfferAsync` (`src/ToolShare.Lending.Domain/Reservations/WaitlistManager.cs`).
- **FR-022 non-regression**: confirmed by diff inspection — `LoanAppService.ReturnAsync`, `LoanManager`, `Reservation.CancelForMaintenance`, and `ReservationManager.CancelForMaintenanceAsync` are untouched; `LoanAppService`'s only change is adding the instance lock to `CheckOutAsync` (11 added lines total in that file, none touching the return path).
- **Concurrency (FR-014/SC-007)**: `IInstanceLock`/`EfCoreInstanceLock` (`pg_advisory_xact_lock`, transaction-scoped, throws if not in a transactional UoW) is taken first in `ReportAsync`, `ReservationAppService.CreateAsync`, and `LoanAppService.CheckOutAsync`, before any Catalog availability read — matches research R4 "Amended". `ReportVersusCheckoutConcurrencyTests`, `ReportVersusReportConcurrencyTests`, `ReportVersusReservationConcurrencyTests` exercise this with real parallel `Task.WhenAll` calls against Testcontainers Postgres (iteration count 3, as stated to be an intentional, user-requested reduction from 5 — not a weakening of the assertions themselves).
- **Migration/CHECK constraint**: `20260928143252_Add_OutOfBand_Maintenance.cs` matches data-model.md exactly (nullable `TriggeringLoanId`, new nullable columns, default-0 `Origin`, `CK_MaintenanceRequests_OriginShape`), applied only via `ToolShare.EntityFrameworkCore.Migrations` / `DbMigrator` — no request-time migration code added. `MaintenanceOriginMigrationTests` verifies pre-feature rows default to `ReturnTriggered`, the CHECK constraint rejects mixed-origin rows via raw SQL, and the filtered unique index still spans both origins.
- **Test-first / no forbidden patterns**: all new tests use Testcontainers Postgres (`*.Application.Tests`) or are DB-free (`*.Domain.Tests`); no `SQLite`/`InMemory`/`Skip` found; existing 004/006 regression tests (`MaintenanceCostReportTests`, `ReservationLifecycleTests`, `WaitlistEntryLifecycleTests`) were extended, not weakened — new cases only, existing assertions intact by inspection.
- **Membership gate / permissions**: `ReportAsync` is `[Authorize(LendingPermissions.Maintenance.Report)]` plus the ambient membership gate; `LibrarianRoleDataSeedContributor` grants it to Librarian (Administrator inherits). `MaintenanceAuthorizationTests`/`NonMemberRefusedTests` cover allow (Librarian), deny (member with no grants), and deny (authenticated non-member).
- **Tier 1 stability**: `catalog-extension.md`'s additive claim holds — the four pre-existing `IToolInstanceCirculationReportingAppService` methods are untouched; `ToolCondition`/`ToolInstanceCirculationState` unchanged.
- **UI/Blazor boundary**: `ReportMaintenance.razor` only imports `ToolShare.Catalog.ToolInstances` (Application.Contracts) and Lending's own contracts; `ToolDetail.razor`'s new "Report damage" link navigates by URL only (Reserve precedent), no project reference added.
- **tasks.md**: only T057 (manual timed walkthrough) is unchecked, matching the stated expectation; no phantom `[X]` completions found for touched requirement IDs — spot-checked FR-001–FR-022 against the corresponding tests and found coverage for each.

REVIEW: APPROVE

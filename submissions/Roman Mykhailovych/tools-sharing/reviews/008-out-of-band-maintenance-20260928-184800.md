# Code review — 008-out-of-band-maintenance (second pass, after convergence)

- **Date**: 2026-09-28 18:48
- **Base ref**: HEAD (`bbbd230`); branch `roman-mykhailovych` has no upstream, so uncommitted changes only (staged + unstaged + untracked)
- **Scope**: whole diff (focus: convergence tasks T059–T062)
- **Feature**: specs/008-out-of-band-maintenance
- **Gate verdict**: PASS (`green-runs/20260928-184331-gate`): Catalog.Domain.Tests 81/81, Lending.Domain.Tests 70/70, Catalog.Application.Tests 80/80, Lending.Application.Tests 140/140, module-boundary audit clean
- **Reviewer**: `toolshare-reviewer` subagent (project agent definition, `.claude/agents/toolshare-reviewer.md`)
- **Previous review**: `reviews/008-out-of-band-maintenance-20260928-183600.md` (APPROVE)

---

Second-pass review of 008-out-of-band-maintenance (convergence round T059–T062), on top of the already-approved first pass.

Scope reviewed this pass: `src/ToolShare.Lending.Blazor/Pages/Lending/MyReservations.razor`, `src/ToolShare.Catalog.Blazor/Pages/Catalog/ToolDetail.razor`, `test/ToolShare.Lending.Application.Tests/Maintenance/OutOfBandReservationCascadeTests.cs` (new), `test/ToolShare.TestBase/PostgreSqlContainerFixture.cs`, `test/README.md`, `specs/008-out-of-band-maintenance/research.md` (R10 amendment), `specs/008-out-of-band-maintenance/tasks.md` (Phase 8 additions T059–T062). Also re-verified the prior pass's full diff stat is unchanged apart from these files plus unrelated out-of-scope churn (`.agent-log/`, `docs/`, `CLAUDE.md`).

Findings

Blocking: none.

Important: none.

Nice-to-have: none new (the prior review's single nice-to-have on `MaintenanceRequestAppService.cs`'s null-forgiving `MemberId!.Value` still stands but is untouched by this round and was already accepted as non-blocking).

Verification notes:
- **T059** (`MyReservations.razor:15-16,22-25`): new "Cancelled"/"Reason" columns bind `context.CancelledAt`/`context.CancellationReason`, both pre-existing on `ReservationDto` (`IReservationAppService.cs:57,59`, unchanged by this diff — confirmed no diff on that file). New test `OutOfBandReservationCascadeTests.The_holder_sees_the_cancellation_and_its_reason_in_their_own_reservations` calls `IMyLendingAppService.GetMyReservationsAsync()` as the holder (`AsMemberWithNoGrants()`) after a Librarian report, and asserts `CancellationReason` equals `MaintenanceRequestAppService.OutOfBandCancellationReason` ("Instance taken out of circulation for maintenance." — verified the literal matches `src/ToolShare.Lending.Application/Maintenance/MaintenanceRequestAppService.cs:27` exactly). Test class also covers the multi-reservation cascade, an untouched reservation on another instance, and asserts no `LendingNotificationDueEto` is raised (FR-009) — good coverage of FR-008/FR-009/US2/AC3, matches testing.md conventions (real app services, Shouldly, `using (AsX())` blocks not held across `await`, no Skip).
- **T060** (`ToolDetail.razor:294-300`): `DescribeStatus` switches on `ToolInstanceCirculationState`, correctly mapping `UnderMaintenance`→"Under maintenance", `OnLoan`→"On loan", preserving `Retired` and the `IsAvailable` fallback. Enum values (`ToolInstanceCirculationState.cs:14-17`) are untouched Tier 1 values — no renumbering, additive-only preserved.
- **T061** (`MyReservations.razor:63-71`): `VisibleWaitlistEntries` filters to `Waiting`/`Offered` by default with a `MudSwitch` toggle to show all; `DescribeOfferState` gives readable labels including `Withdrawn` → "Offer withdrawn (instance under maintenance)". `WaitlistOfferState` enum unchanged by this round (confirmed empty diff on that file in this pass — it was already added in the prior pass).
- **T062**: `PostgreSqlContainerFixture.cs` change is scoped and low-risk — pool cap of 10 per cloned database via `MaxPoolSize`, and `ClearPool` on the *previous* database's connection string only when the next one is cloned, guarded by a lock, with a clear comment that this is safe because tests within an assembly run sequentially. `test/README.md` and `research.md` R10 both document the cause (53300 "too many clients") and the fix accurately, matching the iteration-count reduction (5→3) actually present in `ReportVersus*ConcurrencyTests` (spot-checked previously). No weakening of assertions, no `Skip`, no forbidden patterns per `.claude/rules/testing.md`.
- **tasks.md**: T059–T062 all marked `[X]` with descriptions matching the actual diffs exactly (no phantom completions); T057 (manual walkthrough) correctly left unchecked; T058 (completion) is pending this review as stated.
- Membership gate, module boundaries, schema isolation, append-only history: no changes touch those areas in this round; nothing new to flag.

REVIEW: APPROVE

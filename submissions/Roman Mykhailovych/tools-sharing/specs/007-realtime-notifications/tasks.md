---

description: "Task list for Real-Time In-App Notifications"
---

# Tasks: Real-Time In-App Notifications

**Input**: Design documents from `/specs/007-realtime-notifications/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: MANDATORY. Constitution **Principle V (Test-First Discipline, NON-NEGOTIABLE)** requires xUnit
unit tests (no database) plus application-layer integration tests against real PostgreSQL via
Testcontainers. Test tasks are listed **before** the implementation they cover and must fail first. The
Blazor **rendering** itself (bell/inbox re-render on a signal) is validated by [quickstart.md](./quickstart.md),
exactly as 005 validated its inbox Razor rather than unit-testing rendering.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested, and demoed
independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US3)
- Exact file paths are included in every task

## Path Conventions

Modular monolith at repository root per [plan.md](./plan.md). This feature stays **entirely inside the
existing `ToolShare.Notifications.*` module** — `src/ToolShare.Notifications.Application.Contracts`,
`src/ToolShare.Notifications.Application`, `src/ToolShare.Notifications.Blazor`, and tests in
`test/ToolShare.Notifications.Application.Tests`. Stack: **.NET 10 (`net10.0`) / ABP 10.5.x / PostgreSQL 16
/ Blazor Web App (InteractiveServer, MudBlazor)**. **No new project, no new NuGet package, no EF
migration, no schema change** (research R2, data-model.md).

> **Read before starting**: [research.md](./research.md) R2 (in-process broadcast over the existing
> InteractiveServer circuit — no SignalR hub), R3 (signal **after** UoW commit so a re-query sees the
> change), R4 (signal from the two existing generators), R5 (interface in `Application.Contracts`, singleton
> in `Application`), R6 (thread-safety + exception isolation + test split), and
> [contracts/realtime-inapp.md](./contracts/realtime-inapp.md) for the exact seam and its **two**
> producer-side publishers (the generators, and — for FR-008 — the mark-read app-service methods).

---

## Phase 1: Setup (Baseline)

**Purpose**: Establish a clean, green baseline so later red→green is attributable to this feature. There
is no project/package/skeleton to create.

- [X] T001 Confirm a clean baseline: `dotnet build ToolShare.slnx` succeeds with 0 errors and `dotnet test test/ToolShare.Notifications.Application.Tests/ToolShare.Notifications.Application.Tests.csproj` is green (records that 005's behavior is intact before any change)

---

## Phase 2: Foundational (Broadcaster + Generator Signalling — Blocking Prerequisites)

**Purpose**: The in-process signal producer that **every** user story consumes — the broadcaster
abstraction, its singleton implementation, and the two generators publishing a post-commit signal on a
*new notification*. No UI story can work until this exists.

**⚠️ CRITICAL**: No user-story (Phase 3+) work can begin until this phase is complete.

- [X] T002 [P] Write failing unit tests for the broadcaster (no database) in `test/ToolShare.Notifications.Application.Tests/RealTime/InProcessMemberNotificationBroadcasterTests.cs`: a signal for member A reaches all of A's subscribers and none of B's (FR-002/FR-006); disposing a subscription stops its delivery (no leak); a subscriber that throws is isolated and does not surface to the caller nor block other subscribers (FR-009)
- [X] T003 Create the `IMemberNotificationBroadcaster` interface (namespace `ToolShare.Notifications.RealTime`) in `src/ToolShare.Notifications.Application.Contracts/RealTime/IMemberNotificationBroadcaster.cs` per [contracts/realtime-inapp.md](./contracts/realtime-inapp.md) — `IDisposable Subscribe(Guid memberId, Func<Task> onChanged)` and `Task NotifyAsync(Guid memberId)`
- [X] T004 Implement `InProcessMemberNotificationBroadcaster : IMemberNotificationBroadcaster, ISingletonDependency` in `src/ToolShare.Notifications.Application/RealTime/InProcessMemberNotificationBroadcaster.cs`: thread-safe keyed registry (`ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Func<Task>>>`), snapshot-before-dispatch, catch+log any subscriber exception, `Subscribe` returns an `IDisposable` that removes the callback — makes T002 pass
- [X] T005 [P] Write a failing integration test (real PostgreSQL, shared fixture) in `test/ToolShare.Notifications.Application.Tests/Notifications/GeneratorRealtimeSignalTests.cs`: raising `LendingNotificationDueEto` and `MemberStandingChangedEto` signals the target member **exactly once**; a re-query performed **inside** the signal callback already sees the committed notification (proves post-commit ordering, R3); a different member is never signalled
- [X] T006 [P] Wire post-commit signalling into `src/ToolShare.Notifications.Application/Notifications/LoanNotificationGenerator.cs`: inject `IMemberNotificationBroadcaster` + `IUnitOfWorkManager`; after the insert, register `Current.OnCompleted(() => broadcaster.NotifyAsync(memberId))` (immediate notify if no ambient UoW) — see R3
- [X] T007 [P] Wire the same post-commit signalling into `src/ToolShare.Notifications.Application/Notifications/StandingChangeNotificationGenerator.cs` (identical pattern) — together with T006 makes T005 pass

**Checkpoint**: A *new* notification created for a member now raises exactly one post-commit,
member-scoped, exception-isolated signal — proven by unit + integration tests. UI stories can begin.

---

## Phase 3: User Story 1 - A new notification appears without refreshing (Priority: P1) 🎯 MVP

**Goal**: A member with the inbox open sees a newly generated notification (and its unread state) appear
within a second, no manual refresh, reaching only that member.

**Independent Test**: Sign in as a member with the inbox open, generate a notification for them, and
confirm it appears within a few seconds with no reload; in a second browser as a different member,
confirm nothing appears (quickstart Scenarios A & B).

- [X] T008 [US1] Make the inbox live in `src/ToolShare.Notifications.Blazor/Pages/Notifications/MyNotifications.razor`: resolve the caller's member id once (as `MyNotificationsAppService` does), `Subscribe` in `OnInitializedAsync`, re-query via the existing `ReloadAsync()` on each signal wrapped in `InvokeAsync(StateHasChanged)`, and implement `IDisposable` to unsubscribe on teardown — the signal stays contentless; the list/count come from `IMyNotificationsAppService` (FR-003/FR-004). The inbox's own mark-read already calls `ReloadAsync()`, so it needs no signal to update *itself*
- [ ] T009 [US1] Validate US1 by running quickstart Scenario A (live arrival, SC-001 ≤3 s) and Scenario B (cross-member isolation, FR-002) from [quickstart.md](./quickstart.md)

**Checkpoint**: The inbox updates live for the right member; US1 is independently demoable.

---

## Phase 4: User Story 2 - The unread indicator stays live everywhere (Priority: P2)

**Goal**: A live unread bell in the app toolbar updates from any page as notifications arrive **and as
the member marks things read**, replacing the navigation-time menu-label count.

**Independent Test**: On a page other than the inbox, generate a notification and confirm the toolbar
bell increments live; open the inbox, mark all read, and confirm the bell shows zero from any page
(quickstart Scenario C).

> **Why the mark-read signal lives here** (analyze finding I1): the bell is a *separate component* from
> the inbox, so a mark-read performed on the inbox page cannot update the bell unless it, too, raises a
> broadcaster signal. US2's own independent test ("mark all read → bell shows zero") therefore requires
> T015/T016. US3 later *verifies* the cross-session convergence this same signal also enables.

- [X] T010 [P] [US2] Create the live bell component `src/ToolShare.Notifications.Blazor/Components/NotificationBell.razor`: resolve own member id, read initial `GetUnreadCountAsync()`, `Subscribe` on init, re-query the unread count on each signal (`InvokeAsync(StateHasChanged)`), implement `IDisposable`; render a bell with the count (0 ⇒ no badge) linking to `/notifications/my`
- [X] T011 [US2] Create `NotificationBellToolbarContributor : IToolbarContributor` in `src/ToolShare.Notifications.Blazor/Components/NotificationBellToolbarContributor.cs` that adds `NotificationBell` to `StandardToolbars.Main` (match the exact `ToolbarItem`/context member names to ABP 10.5.x — types confirmed in `Volo.Abp.AspNetCore.Components.Web.Theming.Toolbars`)
- [X] T012 [US2] Register the contributor via `Configure<AbpToolbarOptions>(o => o.Contributors.Add(new NotificationBellToolbarContributor()))` in `src/ToolShare.Notifications.Blazor/NotificationsBlazorModule.cs` `ConfigureServices`
- [X] T013 [US2] Remove the `"(n)"` unread-count suffix (and its `IMyNotificationsAppService` call) from `src/ToolShare.Notifications.Blazor/Menus/NotificationsMenuContributor.cs`, leaving a plain menu link, so the count has one authoritative, live home (the bell) and two indicators can't disagree
- [X] T014 [P] [US2] *Optional*: if the bell needs a tooltip/aria label beyond the existing `Menu:Notifications.MyNotifications` key, add it to `src/ToolShare.Notifications.Domain.Shared/Localization/Notifications/en.json`; otherwise reuse the existing key and skip (no Domain.Shared change)
- [X] T015 [US2] Write a failing integration test (real PostgreSQL) in `test/ToolShare.Notifications.Application.Tests/Notifications/MarkReadSignalTests.cs`: `MarkReadAsync` and `MarkAllReadAsync` each publish exactly one post-commit broadcaster signal for the caller's member (and for no other member), so the bell and other sessions can converge (FR-008)
- [X] T016 [US2] Publish a post-commit signal on read-state change in `src/ToolShare.Notifications.Application/Notifications/MyNotificationsAppService.cs`: inject `IMemberNotificationBroadcaster` + `IUnitOfWorkManager`; after `MarkReadAsync`/`MarkAllReadAsync` mutate state, register `Current.OnCompleted(() => broadcaster.NotifyAsync(memberId))` — makes T015 pass and satisfies FR-008
- [ ] T017 [US2] Validate US2 by running quickstart Scenario C (live indicator from any page increments on generation, and drops to zero on mark-all-read, FR-003/FR-008/SC-002)

**Checkpoint**: The unread bell is live app-wide and reacts to both arrivals and mark-read; US1 and US2
both work independently.

---

## Phase 5: User Story 3 - Never miss or double-show across sessions and reconnections (Priority: P3)

**Goal**: Multiple sessions of the same member converge, read-state propagates across them, and a
dropped-and-restored connection reconciles to the authoritative list — none missed, none doubled.

**Independent Test**: Two tabs as one member both reflect a new notification and converge on the same
count; marking read in one converges the other; a notification generated during a simulated outage is
present (not duplicated) after reconnect; a never-connected member sees everything on next load
(quickstart Scenario D).

> **Mostly emergent** from the Phase 2 broadcaster (member-keyed fan-out, multi-subscriber, tested in
> T002), the US2 mark-read signal (T016), and every component's re-query on (re-)initialization
> (T008/T010). This phase confirms those combine correctly and adds explicit code only if a gap is found.
> Its cross-session *read* convergence assumes US2's T016 is in place.

- [X] T018 [US3] Confirm reconciliation-on-(re)initialization needs no extra code: verify the inbox (T008) and bell (T010) re-query in `OnInitializedAsync` and dispose their subscriptions, so a rebuilt circuit reconciles to the authoritative list (FR-005) and a member's multiple circuits all fan out (FR-006); add an explicit reconnect re-query only if a gap is found — record the finding in the PR description
- [ ] T019 [US3] Validate US3 by running quickstart Scenario D (two-tab convergence, mark-read cross-session convergence, reconnection reconciliation, never-connected-then-signs-in, FR-005/FR-006/FR-008)

**Checkpoint**: All three stories are independently functional; the live view is trustworthy under
multi-session and flaky-connection conditions.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Whole-feature verification and guarding the plan's scope claims.

- [ ] T020 [P] Run the full suite: `dotnet build ToolShare.slnx` (0 errors) and `dotnet test ToolShare.slnx` (all green, including 005's unchanged tests — no regression to generation, dedup, email, or self-service viewing)
- [X] T021 [P] Guard scope: confirm `git diff --stat` touches only `src/ToolShare.Notifications.*` and `test/ToolShare.Notifications.Application.Tests` — **no** EF migration added, **no** new `PackageReference`, **no** other module or `DbMigrator` changed, and **no** new `DeliveryChannel` enum value or delivery-record kind (FR-010; plan's Constitution II/III/VI claims)
- [ ] T022 Run quickstart Scenario E (real-time path failure is harmless): confirm generation, persistence, and email (005) all still succeed and the notification still appears on next load when the live push does nothing (FR-009/FR-010/SC-006)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup — **BLOCKS all user stories** (produces the new-notification signal every story consumes).
- **User Stories (Phase 3–5)**: each depends on Foundational. US1 and US2 are independently testable and can proceed in parallel once Phase 2 is done; US3's cross-session *read* verification assumes US2's mark-read signal (T016).
- **Polish (Phase 6)**: depends on the user stories being complete.

### User Story Dependencies

- **US1 (P1)** — inbox live subscription. Depends on Phase 2 only. No dependency on US2/US3. Self-contained.
- **US2 (P2)** — toolbar bell + menu-count removal **+ mark-read signal (FR-008)**. Depends on Phase 2 only. Now self-contained: its independent test (bell reacts to arrivals *and* mark-read) is fully covered within the story.
- **US3 (P3)** — multi-session/reconnection verification. Depends on Phase 2; its cross-session read-convergence check leverages US2's T016, so build US3 after US2 (which its P3 priority already implies).

### Within Each Story

- Test task(s) before implementation: **T002 before T004**; **T005 before T006/T007**; **T015 before T016**.
- Contracts/interface (T003) before its implementation (T004) and before consumers (T006/T007, T008, T010, T016).
- Story complete and validated (its quickstart scenario) before moving to the next priority.

### Parallel Opportunities

- **Phase 2**: T002 (unit test) ∥ T005 (integration test) can be written together; after T003+T004, T006 ∥ T007 (different generator files).
- **Phase 4**: T010 (bell component) ∥ T014 (optional localization) — different files. T015/T016 (mark-read, a different file: the app-service) can proceed alongside T010–T013.
- **Phase 6**: T020 ∥ T021 — independent checks.
- Once Phase 2 completes, **US1 and US2 can be built in parallel** by different developers (inbox page vs bell/toolbar/menu/app-service — disjoint files).

---

## Parallel Example: Phase 2 (Foundational)

```bash
# Write both failing tests first, together:
Task: "Unit tests for the broadcaster in test/ToolShare.Notifications.Application.Tests/RealTime/InProcessMemberNotificationBroadcasterTests.cs"
Task: "Integration test for generator→signal in test/ToolShare.Notifications.Application.Tests/Notifications/GeneratorRealtimeSignalTests.cs"

# After the interface (T003) + singleton (T004) exist, wire both generators in parallel:
Task: "Post-commit signal in src/ToolShare.Notifications.Application/Notifications/LoanNotificationGenerator.cs"
Task: "Post-commit signal in src/ToolShare.Notifications.Application/Notifications/StandingChangeNotificationGenerator.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 (baseline) → Phase 2 (broadcaster + generator signalling, with its tests green).
2. Phase 3 (US1): inbox live subscription.
3. **STOP and VALIDATE** quickstart Scenarios A & B — a member sees new notifications live in the inbox, isolated per member. Demoable MVP.

### Incremental Delivery

1. Foundation + US1 → the inbox is live (MVP).
2. Add US2 → the unread bell is live app-wide and reacts to arrivals *and* mark-read (FR-008).
3. Add US3 → multi-session convergence, reconnection reconciliation (mostly verification over the pattern).
4. Polish → full suite green, scope guarded (no migration/package/other-module/channel change), degradation confirmed.

### Notes

- [P] = different files, no dependency on an incomplete task.
- The whole feature adds **no persisted data**; the only new "state" is the in-memory broadcaster registry (data-model.md).
- Signal **after** commit (`OnCompleted`) everywhere it is raised (T006/T007/T016) — never inline — so re-queries on other connections see the change (research R3).
- Keep the signal **contentless**; all displayed data comes from `IMyNotificationsAppService` so nothing can leak across members and the count can't drift (FR-002/FR-003).
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.

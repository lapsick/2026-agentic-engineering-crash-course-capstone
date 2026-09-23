# Phase 0 Research: Real-Time In-App Notifications

All decisions below resolve the Technical Context to concrete choices. There were **no
`[NEEDS CLARIFICATION]` markers** in [spec.md](./spec.md); this document records the design decisions the
plan depends on, each with the alternatives weighed and why they were rejected.

---

## R1 — What actually has to change for "live", given the current 005 UI?

**Decision**: Two things are stale-until-navigation in 005 and must become push-driven:

1. **The inbox list** (`MyNotifications.razor`) reloads only in `OnInitializedAsync` and after a
   mark-read click.
2. **The unread indicator** is **not a component** — `NotificationsMenuContributor` appends `"(n)"` to
   the *menu label string*, computed once while the menu tree is built (on navigation). A string in a
   menu item cannot be re-rendered on demand.

So "make it live" = (a) let the inbox re-query on a pushed signal, and (b) replace the menu-label count
with a **real interactive component** that can re-render — a toolbar bell. Everything else in 005
(generation, persistence, email, dedup, read-state) is already correct and stays untouched.

**Rationale**: This scopes the feature precisely to timeliness and identifies the one structural change
(menu-label → component) that is easy to miss. Confirmed by reading `MyNotifications.razor` and
`NotificationsMenuContributor.cs`.

**Alternatives considered**: *Keep the count in the menu label and force a menu rebuild on push* — Blazor
menu contributors don't offer a per-user "rebuild now" hook tied to a server event, and the menu is not
an interactive component under our control; rejected as fighting the framework.

---

## R2 — Transport: in-process broadcast over the Blazor circuit vs a SignalR hub

**Decision**: **In-process singleton broadcaster** that Blazor components subscribe to; the render is
delivered by the **existing Blazor InteractiveServer circuit**. Do **not** add
`Volo.Abp.AspNetCore.SignalR` or a custom hub.

**Rationale**:
- The host runs Blazor **InteractiveServer** (Constitution I; `AddInteractiveServerComponents()` verified
  in `ToolShareBlazorModule`). Each interactive user therefore already has a live server-side circuit
  that streams UI diffs. Calling `StateHasChanged()` on a component *is* a server-to-browser push over
  that circuit — a second SignalR hub would duplicate transport that already exists.
- Zero new packages (matches 005's "no new packages" ethos and Principle I's "prefer the fixed stack").
- The deployment is **single-host** (one `app` service in `docker-compose.yml`, no replicas), so an
  in-process registry reaches every relevant circuit.

**Alternatives considered**:
- *A dedicated ASP.NET Core / ABP SignalR hub with client JS and user groups* — adds a package, a hub, a
  client subscription, and a second transport, buying nothing for a Blazor Server app on a single host.
  Its only real advantage (reaching non-Blazor clients, or scaling across hosts with a backplane) is out
  of scope. Rejected under Governance's "prefer the simpler option."
- *ABP's built-in notification system* — the free/OSS line has no turnkey real-time Blazor notification UI
  to adopt; it would still come down to SignalR + our own components. No saving. Rejected.

**Recorded constraint (boundary note 1)**: a horizontally-scaled deployment would need a backplane
(e.g. Redis) so a signal raised on host A reaches a circuit on host B. Explicitly out of scope; noted so a
future scale-out doesn't silently regress correctness.

---

## R3 — Signal timing: publish after the unit of work commits

**Decision**: Each generator publishes the signal from **`IUnitOfWorkManager.Current.OnCompleted(...)`**,
i.e. **after** the ambient transaction commits, not inline right after `InsertAsync`.

**Rationale**: A subscriber reacts by re-querying through `IMyNotificationsAppService`, which opens its
**own** unit of work / connection. If the signal fired before commit, that re-query — on a different
connection under normal read-committed isolation — would not yet see the row, and the member's count could
lag by one until their *next* action. `OnCompleted` guarantees the row is durably visible before any
subscriber re-queries. If there is no ambient UoW (defensive), publish immediately.

**Rationale for re-query rather than pushing the content**: the signal is intentionally contentless (just
`MemberId`). Re-querying keeps the persisted store the single source of truth (FR-003/FR-004), so a live
count can never drift from the real one, and the push path carries no notification data that could leak
(FR-002). The unread re-query is a single indexed `GetUnreadCountForMemberAsync` — cheap.

**Alternatives considered**:
- *Publish inline after `InsertAsync(autoSave:true)`* — the row is flushed within the transaction but not
  committed; cross-connection re-query may miss it. Rejected (the exact bug `OnCompleted` prevents; the
  integration test in R6 asserts against it).
- *Push the notification content in the signal to avoid a re-query* — reintroduces the possibility of the
  pushed value and the stored value disagreeing, and puts one member's content on a channel other circuits
  subscribe to. Rejected for the source-of-truth and isolation guarantees above.

---

## R4 — Where the signal is raised, and keeping the two generators the single choke point

**Decision**: Raise the signal **from the two existing generators** (`LoanNotificationGenerator`,
`StandingChangeNotificationGenerator`) — the exact points that already know the `MemberId` and already
create the notification — via a shared one-liner (inject `IMemberNotificationBroadcaster`, register the
`OnCompleted` callback after the insert).

**Rationale**: The generators are already the sole creators of `Notification` rows (research R4 of 005),
so signalling there guarantees one signal per created notification, for the right member, with no new
event type. It is a minimal, additive change to two files.

**Alternatives considered**:
- *React to ABP entity-change events (`ILocalEventHandler<EntityCreatedEventData<Notification>>`) to
  decouple real-time from the generators entirely* — attractive (touches zero generator code) but relies
  on entity-event publication being enabled for the `NotificationsDbContext` and on subtle
  during-vs-after-UoW timing, and would still need its own `OnCompleted` for the re-query guarantee. More
  magic, less certain, no smaller. Rejected in favour of the explicit, obviously-correct choke point.

---

## R5 — Placement of `IMemberNotificationBroadcaster` across the module's layers

**Decision**: **Interface in `Notifications.Application.Contracts`** (new `RealTime/` folder),
**singleton implementation in `Notifications.Application`** (`ISingletonDependency`), **subscribers in
`Notifications.Blazor`**.

**Rationale**: `ToolShare.Notifications.Blazor` references **only** `Application.Contracts` (verified in
its `.csproj`), while the publisher lives in `Application`. The one assembly both the Blazor subscriber
and the Application publisher already share is `Application.Contracts`. Placing the interface there — as a
**Tier 2, module-internal** abstraction — lets both sides depend on it with no new project and no widening
of any module boundary. The concrete registry is pure in-memory infrastructure and belongs in
`Application`, wired to the interface by ABP conventional registration.

**Alternatives considered**:
- *Interface in `Domain.Shared`* — `Domain.Shared` is for enums/constants/ETOs, not a live service
  abstraction; and a UI-push contract has nothing to do with the domain. Rejected.
- *Have Blazor reference `Application` directly to see a concrete broadcaster* — breaks the module's own
  layer flow (Blazor → Contracts only) for no benefit. Rejected.

**Stability**: the interface is never referenced by another module (like 005's `IMyNotificationsAppService`)
— Tier 2. No Tier 1 surface changes anywhere.

---

## R6 — Thread-safety, exception isolation, and how this is tested (Principle V)

**Decision**:
- The broadcaster holds a **thread-safe keyed registry** (`ConcurrentDictionary<Guid,
  ConcurrentDictionary<Guid, Func<Task>>>`), snapshots subscribers before dispatch, and **catches and
  logs any subscriber-callback exception** so publishing is best-effort and can never propagate into the
  generator's post-commit phase (FR-009). `Subscribe` returns an `IDisposable` that removes the callback
  (called from the component's `Dispose`, i.e. circuit teardown) — no leaked subscriptions.
- **Unit tests (no database)** cover: signal reaches all subscribers for a member and none of another
  member's (FR-002/FR-006); disposing a subscription stops its delivery; a throwing subscriber does not
  prevent the others or surface to the caller (FR-009).
- **Integration tests (real PostgreSQL, existing fixture)** extend 005's generator tests: raising
  `LendingNotificationDueEto` / `MemberStandingChangedEto` signals the target member **exactly once**, and
  a re-query performed **inside** the signal callback already sees the committed notification (proving the
  R3 post-commit ordering), and a *different* member is never signalled.

**Rationale**: The broadcaster is application infrastructure with no DB dependency, so fast unit tests are
the right tool (Principle V requires *integration* tests specifically for DB-touching application
behaviour, which the generator wiring is — hence its integration coverage). The Blazor re-render on signal
is validated manually in [quickstart.md](./quickstart.md), matching how 005 validated its inbox rendering
rather than unit-testing Razor.

**Alternatives considered**: *`lock`-based registry* — simpler to read but serialises every publish; the
concurrent-dictionary snapshot avoids holding a lock across user callbacks (which do `InvokeAsync`).
Either is acceptable at this scale; the concurrent form is chosen to keep a slow circuit from blocking
another member's signal.

---

## Resolved unknowns summary

| Unknown | Resolution |
|---------|------------|
| New transport/package? | No — in-process broadcast over the existing InteractiveServer circuit (R2) |
| New persisted data / migration? | No — nothing written; re-query the 005 store (R3, data-model.md) |
| When to signal? | After UoW commit via `OnCompleted`, so re-queries see the row (R3) |
| Where to signal from? | The two existing generators — the sole notification creators (R4) |
| Where does the contract live? | `Application.Contracts` (Tier 2); impl singleton in `Application` (R5) |
| Live indicator home | A toolbar `NotificationBell` component via `IToolbarContributor`; menu-label count removed (R1) |
| Multi-instance? | Out of scope; single-host today; backplane needed for scale-out (R2, boundary note 1) |
| Test strategy | Unit (broadcaster) + integration (generator→signal post-commit); UI by quickstart (R6) |

# Implementation Plan: Real-Time In-App Notifications

**Branch**: `007-realtime-notifications` | **Date**: 2026-08-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-realtime-notifications/spec.md`

## Summary

Make the **existing** in-app notification channel live. Today a `Notification` row is created by one of
005's two `ILocalEventHandler` generators (`LoanNotificationGenerator`,
`StandingChangeNotificationGenerator`), and a member only sees it — and their unread count — on the next
page load: the inbox reloads in `OnInitializedAsync`, and the unread indicator is a **string appended
to a menu label** (`NotificationsMenuContributor`) recomputed only when the menu is rebuilt on
navigation. This feature closes that timeliness gap and nothing else: when a notification is created for
a member who has the app open, their inbox and unread indicator update within a second, reaching only
that member, without a manual refresh.

Because the app is **Blazor Web App in InteractiveServer mode** (Constitution I), every interactive user
already holds a live server-side circuit that streams UI diffs to their browser. So the entire feature
is one small in-process seam over that circuit — **no new SignalR hub, no new NuGet package, no new
persisted data, no schema, no migration, no new integration event** — argued in [research.md](./research.md):

1. **An in-process singleton broadcaster** (`IMemberNotificationBroadcaster`, interface in
   `Application.Contracts`, singleton impl in `Application`) keyed by `MemberId`. Interactive Blazor
   components subscribe with **their own** member id (resolved from `CurrentUser` exactly as
   `MyNotificationsAppService` already does) and receive a contentless "your notifications changed"
   signal; they then re-query through the existing `IMyNotificationsAppService`. The signal carries no
   notification content, so the push path itself cannot leak another member's data (FR-002), and the
   re-query stays the single source of truth (FR-003/FR-004).

2. **The two generators publish that signal after their unit of work commits**, via
   `IUnitOfWorkManager.Current.OnCompleted(...)`, so a subscriber that immediately re-queries on another
   connection sees the just-committed row (research R3). Publishing is best-effort and exception-isolated
   inside the broadcaster, so a failing subscriber can never delay or fail the loan/member operation that
   triggered it (FR-009).

3. **A live unread bell replaces the menu-label count as the real-time indicator**, mounted through
   ABP's theming toolbar seam — a `NotificationBellToolbarContributor : IToolbarContributor` (verified
   present: `AbpToolbarOptions`/`IToolbarContributor`/`StandardToolbars` in
   `Volo.Abp.AspNetCore.Components.Web.Theming.Toolbars`) adds a `NotificationBell` component to
   `StandardToolbars.Main`, visible from every page (FR-003, US2). The inbox page subscribes to the same
   broadcaster for its live list (US1). Both implement `IDisposable` and unsubscribe on circuit teardown.

Multi-session (FR-006) and reconnection reconciliation (FR-005) fall out of the pattern rather than
needing dedicated code: every circuit for a member subscribes under the same key so all are signaled,
and every component re-queries the authoritative list on (re-)initialization, so a brief drop or a full
circuit rebuild converges to the persisted truth — which remains authoritative for a member who had no
live session at all (FR-004).

**Read-state also signals (FR-008).** Marking read is a second thing that changes a member's unread
count, and — because the toolbar bell is a *different component* from the inbox — it must be pushed just
like a new notification, otherwise a member's own bell would not react to a mark-read they performed on
the inbox page (and their *other* sessions could not converge either). So `MyNotificationsAppService`'s
`MarkReadAsync`/`MarkAllReadAsync` publish the same contentless, post-commit broadcaster signal for the
member. This is producer-side, reuses the exact `OnCompleted` pattern the generators use, and is why the
bell's mark-read behaviour is built as part of **US2** (the story that introduces the separate
indicator), with US3 then verifying the cross-session convergence it also enables.

Tests follow Principle V: the broadcaster's subscribe/publish/isolate/unsubscribe semantics are covered
by fast **no-database unit tests**; the generator→broadcaster wiring — right member, exactly once,
signalled only *after* the row is committed and queryable, never for another member — is covered by
**integration tests against real PostgreSQL** through the existing `PostgreSqlContainerFixture`,
extending 005's generator tests. The Blazor rendering itself (bell + inbox re-render on signal) is
validated by [quickstart.md](./quickstart.md), consistent with how 005's inbox Razor is not unit-tested
for rendering.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`), SDK pinned by `global.json` — unchanged.

**Primary Dependencies**: ABP Framework 10.5.x (free/open-source only). **No new NuGet packages.** Uses
only already-resolved surfaces: `Volo.Abp.Ddd.Application`/`.Application.Contracts` and
`Volo.Abp.Uow` (`IUnitOfWorkManager.OnCompleted`) already used across the solution; ABP's Blazor
theming toolbar API (`AbpToolbarOptions`, `IToolbarContributor`, `StandardToolbars`) is **[verified]**
present in `Volo.Abp.AspNetCore.Components.Web.Theming` (10.4.1, transitively via
`Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor` 10.5.0 that `ToolShare.Notifications.Blazor`
already references). Real-time transport is the Blazor InteractiveServer circuit itself — already
configured in the host (`AddInteractiveServerComponents()` / `.AddInteractiveServerRenderMode()`), no
`Volo.Abp.AspNetCore.SignalR` hub added.

**Storage**: PostgreSQL 16 — **untouched**. This feature adds no entity, no column, no schema, no index,
and **no EF migration**. It reads existing `notifications`-schema data only through
`IMyNotificationsAppService` (005), never with new queries.

**Testing**: xUnit + Shouldly + NSubstitute; `Testcontainers.PostgreSql` via the shared
`PostgreSqlContainerFixture`. Broadcaster unit tests need no fixture (pure in-memory). Generator→broadcaster
integration tests reuse the existing Notifications application-test host; no new test project.

**Build tooling**: unchanged — no `dotnet-ef` invocation (no migration). Plain `dotnet build` /
`dotnet test`.

**Target Platform**: Linux containers via `docker compose` (single `app` + `postgres`); developer
machines via the `dotnet` CLI.

**Project Type**: Modular-monolith web application — single ASP.NET Core host serving a server-rendered
Blazor (MudBlazor theme) UI.

**Performance Goals**: from notification commit to the member's indicator updating is one in-process
callback plus one indexed `GetUnreadCountForMemberAsync` re-query over the existing circuit — sub-second
in practice, comfortably within SC-001's 3-second bound. The push adds no measurable cost to the
triggering loan/member operation: it is a post-commit, exception-isolated, non-blocking signal.

**Constraints**: single-host deployment (one `app` service in `docker-compose.yml`, no replicas), which
is what makes an in-process broadcaster correct; a multi-instance scale-out would need a SignalR
backplane and is explicitly out of scope (Assumptions, research R2). No new cross-module coupling; no
cross-schema access; ABP Commercial forbidden; builds/tests through the `dotnet` CLI; the running app
never mutates its own schema (trivially satisfied — no schema change at all).

**Scale/Scope**: single community, single tenant, tens of concurrent circuits. Deliverable: **1 new
interface** (`IMemberNotificationBroadcaster`, Tier 2) + its singleton impl, **3 changed application
files** (the two generators plus `MyNotificationsAppService` — one post-commit signal each; the last for
FR-008), **2 new Blazor components** (`NotificationBell` + `NotificationBellToolbarContributor`), a
live-subscription change to the existing inbox page and the menu-count removal, plus **1 new unit-test
class** and **new integration tests** in the existing Notifications test project. No new project, no new
package, no migration.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [.specify/memory/constitution.md](../../.specify/memory/constitution.md) v2.0.0.

| # | Principle | Verdict | How this plan satisfies it |
|---|-----------|---------|----------------------------|
| I | Fixed Stack (NON-NEGOTIABLE) | **PASS** | Same .NET 10 / ABP 10.5.x / Blazor InteractiveServer / PostgreSQL 16 stack; **zero** new packages. Real-time uses the InteractiveServer circuit already mandated by Principle I, not a new transport (research R2). |
| II | Modular Monolith With Strict Boundaries | **PASS** | Everything lives inside the Notifications module. The broadcaster interface sits in `Notifications.Application.Contracts` (Tier 2, module-internal) and is consumed only by `Notifications.Application` (publisher) and `Notifications.Blazor` (subscriber) — no other module is referenced or touched. No new integration event; no Catalog/Membership/Lending change. |
| III | Schema-Level Data Isolation; No Cross-Module FKs | **PASS** | No data at all is added — no table, column, schema, or FK. The broadcast is transient in-memory state, gone at process exit; the persisted `notifications` schema (005) is read only through 005's own app service. |
| IV | Append-Only History | **PASS** | Nothing is written, so nothing is mutated or deleted. The authoritative append-only `Notification`/`NotificationDeliveryRecord` history (005) is unchanged; real-time only surfaces existing rows sooner (FR-007/FR-010). |
| V | Test-First Discipline (NON-NEGOTIABLE) | **PASS** | Broadcaster pub/sub semantics: fast unit tests, no database. Generator→broadcaster wiring (right member, once, post-commit-visible, isolation): integration tests on real PostgreSQL via the existing Testcontainers fixture. UI re-render validated by quickstart, as 005 did for its inbox. |
| VI | IDE-Agnostic, Container-First | **PASS** | No migration, no IDE step; `dotnet build`/`dotnet test` only. Runs in the same single-host `docker compose` topology; the single-instance assumption is documented, not a hidden requirement. |

**Technology & Architecture Constraints check**: auth/authorization unchanged (ABP Identity + the
membership gate; the bell and inbox resolve the caller through `CurrentUser` exactly as 005 does) —
**PASS**. **Background Workers/Jobs**: this feature adds **none** — the real-time signal is a
synchronous, post-commit, in-process callback on the thread that created the notification, not scheduled
or queued work, so the constitution's "background work uses ABP Background Workers/Jobs" clause is not
engaged (there is no background work here) — **PASS**. Pure logic (the broadcaster's keyed dispatch) has
no EF/ABP-infra dependency and is unit-testable in isolation — **PASS**. No read model is rebuilt from
events; the live view is a direct re-query of the authoritative store, not a maintained projection —
**PASS**.

**Development Workflow check**: the one new contract (`IMemberNotificationBroadcaster`) is defined in the
same feature as its only consumers (this module's Application + Blazor) — **PASS**. This plan adds no
functional requirements; they stay in [spec.md](./spec.md) — **PASS**.

### Boundary notes (recorded, not violations)

1. **The in-process broadcaster assumes a single application instance.** This is not new scope creep: the
   product is single-community, single-tenant, and shipped as one `app` container (`docker-compose.yml`).
   A horizontally-scaled deployment would require a SignalR backplane (e.g. Redis) so a signal raised on
   one host reaches a circuit on another; that is recorded in the spec's Assumptions ("No offline… ",
   single-host) and in research R2 as explicitly out of scope, exactly as prior features treated
   multi-instance concerns.
2. **The interface lives in `Application.Contracts` though it is an infrastructure abstraction, not a
   DTO/app-service.** This is the least-bad placement given ABP's reference flow: `Notifications.Blazor`
   references only `Application.Contracts` (verified), while the publisher is in `Application`. Putting the
   contract in `Application.Contracts` (Tier 2, module-internal) keeps both sides depending on the one
   shared, non-`Domain` assembly they already share, with no new project. It is never exposed to another
   module (Principle II), the same module-internal scoping 005's own `IMyNotificationsAppService` has.
3. **The unread indicator moves from a menu-label string to a toolbar component.** The existing
   `NotificationsMenuContributor` count is recomputed only on navigation and cannot be pushed to; a live
   indicator must be an interactive component. Plan: add the live `NotificationBell` to the theme toolbar
   and reduce the menu item to a plain link (drop the `(n)` suffix) so the count has one authoritative,
   live home rather than two that can disagree. This is a UI relocation within `Notifications.Blazor`,
   touching no other module.

**Post-Phase 1 re-evaluation**: re-run after [data-model.md](./data-model.md) and [contracts/](./contracts/)
were written — all six verdicts still **PASS**. Phase 1 confirmed there is no entity, no schema change,
and no cross-module surface: the only published-style artifact is one module-internal (Tier 2) interface
whose signature exposes a single `Guid MemberId` and an `IDisposable` subscription — no `Domain` type
crosses any boundary. Boundary note 1 (single-instance) was re-checked against Principle VI and remains a
documented deployment assumption, not a violation.

## Project Structure

### Documentation (this feature)

```text
specs/007-realtime-notifications/
├── plan.md                 # This file (/speckit-plan output)
├── research.md             # Phase 0 output — decisions with alternatives
├── data-model.md           # Phase 1 output — states plainly: no persisted entities; transient signal
├── quickstart.md           # Phase 1 output — run & validate the live update by hand + test commands
├── contracts/              # Phase 1 output
│   ├── README.md           # Index, stability tier, placement rationale
│   └── realtime-inapp.md   # IMemberNotificationBroadcaster + the toolbar/bell UI contract
├── checklists/
│   └── requirements.md     # Pre-existing spec quality checklist (16/16)
└── tasks.md                # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── ToolShare.Notifications.Application.Contracts/
│   └── RealTime/
│       └── IMemberNotificationBroadcaster.cs      # NEW — Subscribe(memberId, callback):IDisposable;
│                                                   #   NotifyAsync(memberId). Tier 2, module-internal.
├── ToolShare.Notifications.Application/
│   ├── RealTime/
│   │   └── InProcessMemberNotificationBroadcaster.cs  # NEW — singleton (ISingletonDependency),
│   │                                                   #   thread-safe keyed registry, exception-isolated
│   │                                                   #   publish (FR-009)
│   └── Notifications/
│       ├── LoanNotificationGenerator.cs           # CHANGED — after insert, register OnCompleted →
│       │                                           #   broadcaster.NotifyAsync(memberId)
│       ├── StandingChangeNotificationGenerator.cs # CHANGED — same one-line post-commit signal
│       └── MyNotificationsAppService.cs           # CHANGED — post-commit signal on MarkRead/MarkAllRead
│                                                   #   so the bell + other sessions converge (FR-008)
├── ToolShare.Notifications.Blazor/
│   ├── Components/
│   │   ├── NotificationBell.razor                 # NEW — interactive toolbar bell: live unread count,
│   │   │                                           #   subscribes on init, disposes on teardown
│   │   └── NotificationBellToolbarContributor.cs  # NEW — IToolbarContributor → StandardToolbars.Main
│   ├── NotificationsBlazorModule.cs               # CHANGED — Configure<AbpToolbarOptions> add contributor
│   ├── Menus/NotificationsMenuContributor.cs      # CHANGED — drop the "(n)" label count (now the bell's job)
│   └── Pages/Notifications/MyNotifications.razor  # CHANGED — subscribe to broadcaster for live list;
│                                                   #   IDisposable unsubscribe
│
└── ToolShare.Blazor/, ToolShare.Catalog.*/, ToolShare.Membership.*/, ToolShare.Lending.*/,
                                                   # UNCHANGED — no other module touched; no DbMigrator change
    ToolShare.DbMigrator/

test/
├── ToolShare.Notifications.Domain.Tests/          # UNCHANGED (broadcaster is app infra, not domain)
│                                                   #   — see note below
├── ToolShare.Notifications.Application.Tests/
│   ├── RealTime/
│   │   └── InProcessMemberNotificationBroadcasterTests.cs  # NEW — pure unit tests, no fixture
│   └── Notifications/
│       └── ...Generator real-time signalling tests         # NEW — integration on real PostgreSQL:
│                                                            #   signalled once, only for the target member,
│                                                            #   only after the row is committed/queryable
└── ToolShare.TestBase/                            # UNCHANGED — fixture reused as-is
```

**Structure Decision**: Stay entirely within the existing `ToolShare.Notifications.*` module — no new
project and no new package. New code is one interface (Contracts) + one singleton (Application) + two
Blazor components, plus a one-line post-commit signal in each of the two existing generators and in
`MyNotificationsAppService`'s mark-read methods (FR-008), and the toolbar/menu wiring. The broadcaster's fast unit tests and the generator integration tests both land in
the **existing** `ToolShare.Notifications.Application.Tests` project (the unit tests simply don't take the
`PostgreSqlContainerFixture`); no `Domain.Tests` change, since real-time delivery introduces no domain
rule. Every other module — and `DbMigrator` — is untouched.

## Complexity Tracking

> No constitutional violations to justify. The feature is deliberately the smallest increment on top of
> 005: it adds behavior (timeliness) without adding data, a package, a project, a migration, an
> integration event, or a cross-module reference. The only judgment calls — an in-process broadcaster
> (vs a SignalR hub) and placing its interface in `Application.Contracts` — are recorded as boundary
> notes above and argued in research R2/R5, each choosing the simpler option that keeps module boundaries
> intact, as Governance directs.

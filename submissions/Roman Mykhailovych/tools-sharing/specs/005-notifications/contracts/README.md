# Notifications Contracts

**Feature**: `005-notifications` | **Date**: 2026-08-04

Notifications exposes coupling through the same two channels every module in this codebase is limited
to, per Constitution Principle II:

1. **Interfaces and DTOs** in `ToolShare.Notifications.Application.Contracts`
2. **Integration events** on `ILocalEventBus` — here, only as a **consumer** (see below)

Unlike every prior module, Notifications is the first whose implementation requires **extending an
already-shipped module's** published boundary (Membership's) purely to *read* one more field, not to
add a new capability — see [membership-extension.md](./membership-extension.md).

| Document | Scope | Requirement |
|---|---|---|
| [notifications-app-services.md](./notifications-app-services.md) | The module-internal service surface consumed by the Notifications Blazor UI | FR-012, FR-013, FR-014, FR-015 |
| [notifications-permissions.md](./notifications-permissions.md) | The permission boundary — self-service scoping plus one Administrator-only audit permission | FR-014, FR-015 |
| [membership-extension.md](./membership-extension.md) | The one additive field this feature adds to **Membership**'s Tier 1 `MemberStandingDto` | FR-008, research R1 |

## Stability tiers

**Tier 1 — Public (frozen once shipped).** Notifications publishes **no Tier 1 surface of its own** in
this feature — no downstream module or feature currently depends on anything Notifications produces
(plan.md boundary note 2). It only *consumes* two other modules' already-frozen Tier 1 surfaces:
Lending's `LendingNotificationDueEto` and Membership's `MemberStandingChangedEto`, unchanged by this
feature, plus the one new field this feature adds to Membership's `MemberStandingDto`.

**Tier 2 — Module-internal.** Everything in
[notifications-app-services.md](./notifications-app-services.md). Consumed only by
`ToolShare.Notifications.Blazor`. No other module may reference these types.

## Placement rules

| Type | Project | Why |
|---|---|---|
| `NotificationKind`, `DeliveryChannel`, `DeliveryStatus`, error codes, L10n | `ToolShare.Notifications.Domain.Shared` | ABP's home for behavior-free shared types |
| App-service interfaces, DTOs, `NotificationsPermissions` | `ToolShare.Notifications.Application.Contracts` | The module's own boundary (Tier 2 — no downstream consumer) |
| `Notification`, `NotificationDeliveryRecord`, repository interface | `ToolShare.Notifications.Domain` | **Never** referenced across modules |
| `NotificationsDbContext`, EF configurations, the repository implementation | `ToolShare.Notifications.EntityFrameworkCore` | **Never** referenced across modules |
| `LoanNotificationGenerator`, `StandingChangeNotificationGenerator`, `SendEmailNotificationJob` | `ToolShare.Notifications.Application` | Where `ILocalEventHandler<T>` implementations and background jobs live in every prior module (mirrors `MemberStandingCacheInvalidator`'s placement in `ToolShare.Membership.Application`) |
| `Email` field on `MemberStandingDto` (the **inbound** read this feature depends on) | **Membership's** `ToolShare.Membership.Application.Contracts` | Declared and populated entirely within Membership — see [membership-extension.md](./membership-extension.md); Notifications only reads it |

## The one inverted-shaped dependency this feature relies on

Unlike Lending (004), which sat on both ends of "downstream module reports a fact into an upstream
module," Notifications sits on only the **consuming** end of every relationship it has:

- Notifications **subscribes to** Lending's `LendingNotificationDueEto` (004) — a local event handler,
  not an app-service call; Lending remains completely unaware Notifications exists.
- Notifications **subscribes to** Membership's `MemberStandingChangedEto` (003) — same shape.
- Notifications **calls into** Membership's `IMemberStandingAppService` (003, extended here) and
  Catalog's `IToolInstanceLookupAppService` (002) to resolve display content — both already-published,
  read-only Tier 1 lookups; neither module gains a new inbound port because of Notifications, unlike
  Catalog's and Membership's own reporting contracts that Lending required.

## How the module boundary is verified

A `PublicContract`-style integration test in `ToolShare.Notifications.Application.Tests` (mirroring
002's, 003's, and 004's own boundary tests) raises a `LendingNotificationDueEto` and a
`MemberStandingChangedEto` directly (simulating what Lending's and Membership's own domain layers would
raise) and asserts the expected `Notification` rows and delivery records appear — importing **no**
`ToolShare.Lending.Domain…`, `ToolShare.Membership.Domain…`, or `ToolShare.Catalog.Domain…` namespace,
only each module's `Domain.Shared` (for the event types themselves) and `Application.Contracts` (for
the lookups).

# Contracts — 007 Real-Time In-App Notifications

This feature publishes **no new cross-module (Tier 1) surface** and **no new integration event**. It adds
exactly one **module-internal (Tier 2)** abstraction, consumed only by the Notifications module's own
Application (publisher) and Blazor (subscriber) layers, plus a UI seam (the theme toolbar) it plugs a live
component into.

## Stability tiers (per `specs/002-catalog-foundation/contracts/README.md`)

| Artifact | Tier | Consumers | Notes |
|---|---|---|---|
| `IMemberNotificationBroadcaster` | **Tier 2 — module-internal** | `Notifications.Application`, `Notifications.Blazor` only | New. In-process signal; see [realtime-inapp.md](./realtime-inapp.md). Never referenced by another module (Principle II), like 005's `IMyNotificationsAppService`. |
| `NotificationBell` + `NotificationBellToolbarContributor` | Tier 2 (UI) | The host theme toolbar | New. Mounts into `StandardToolbars.Main` via ABP's `IToolbarContributor`/`AbpToolbarOptions`. |
| `IMyNotificationsAppService` (005) | Tier 2 (existing) | This module's Blazor | **Reused unchanged** — the re-query path for the live view. No signature change. |

## What is explicitly **not** changed

- **No Tier 1 change** to any module. Catalog, Membership, and Lending contracts/events are untouched;
  unlike 005 (which added `MemberStandingDto.Email`), this feature needs nothing new from another module.
- **No new integration event / ETO.** The real-time signal is an in-process, in-memory call, never an
  `ILocalEventBus`/`IDistributedEventBus` message and never persisted.
- **No new permission.** The bell and inbox authorize through the existing enrolment gate and
  `CurrentUser`, exactly as 005's inbox does; there is no new `NotificationsPermissions` entry.
- **No DTO change.** `NotificationDto` and the `IMyNotificationsAppService` method set are reused verbatim.

## Contract test intent

The boundary assertion for this feature is the **absence** of coupling: the real-time signal exposes only
a `Guid MemberId` and an `IDisposable`, so no `Domain` type or another module's type crosses it. The
integration tests (see [../research.md](../research.md) R6) prove a subscriber is signalled for the correct
member exactly once and only after the notification is committed — without importing any other module's
`Domain`/`EntityFrameworkCore` namespace, the same way 005's tests do.

# Contract: In-App Real-Time Signal & Live Indicator

**Tier 2 — module-internal.** Lives in `ToolShare.Notifications.Application.Contracts` (interface) and
`ToolShare.Notifications.Blazor` (UI). Consumed only within the Notifications module. Not a cross-module
contract; not an integration event.

---

## 1. `IMemberNotificationBroadcaster` (interface → `Application.Contracts/RealTime/`)

The in-process seam between "a notification was created/changed for a member" (publisher:
`Application`) and "that member's live circuits should re-render" (subscriber: `Blazor`).

```csharp
namespace ToolShare.Notifications.RealTime;

/// <summary>
/// In-process, single-host push seam (research R2). A live Blazor circuit subscribes for its OWN
/// member id; generators publish a contentless signal after their unit of work commits (research R3).
/// Best-effort and exception-isolated — never blocks or fails the operation that triggered it (FR-009).
/// Registered as a singleton (ISingletonDependency).
/// </summary>
public interface IMemberNotificationBroadcaster
{
    /// <summary>
    /// Register interest in a single member's notification changes. Returns a handle that MUST be
    /// disposed on circuit/component teardown to unsubscribe. The callback is invoked (with no payload)
    /// whenever <see cref="NotifyAsync"/> runs for this <paramref name="memberId"/>.
    /// </summary>
    IDisposable Subscribe(Guid memberId, Func<Task> onChanged);

    /// <summary>
    /// Publish a contentless "your notifications changed" signal to every current subscriber for
    /// <paramref name="memberId"/>. Subscriber exceptions are caught and logged, never propagated.
    /// </summary>
    Task NotifyAsync(Guid memberId);
}
```

**Semantics** (enforced by tests, research R6):

- **Isolation by member (FR-002/FR-006)** — `NotifyAsync(A)` invokes every subscriber registered for `A`
  and no subscriber registered for any other member. All of `A`'s subscribers (multiple tabs/circuits)
  fire.
- **Contentless** — the signal carries no notification data; subscribers re-query
  `IMyNotificationsAppService` (source of truth, FR-003/FR-004).
- **Best-effort, non-blocking (FR-009)** — a subscriber that throws or is slow neither fails nor delays
  the publish nor the generator's post-commit phase.
- **No leak** — the returned `IDisposable`, disposed by the component, removes the subscription.

**Publishers** — two producer-side call sites raise the identical signal:

1. **The two generators**, after `InsertAsync(autoSave: true)` — a *new* notification (FR-001).
2. **`MyNotificationsAppService.MarkReadAsync`/`MarkAllReadAsync`**, after the read-state mutation — an
   *unread-count* change, so the member's bell and their other sessions converge (FR-008).

Both use the same post-commit pattern:

```csharp
// Signal AFTER commit so a subscriber's re-query (its own connection) sees the change (research R3).
_unitOfWorkManager.Current?.OnCompleted(() => _broadcaster.NotifyAsync(memberId))
    // Defensive: no ambient UoW → signal immediately.
    ?? await _broadcaster.NotifyAsync(memberId);
```

---

## 2. `NotificationBell` + `NotificationBellToolbarContributor` (→ `Blazor/Components/`)

The live unread indicator, replacing the navigation-time menu-label count (research R1).

- **`NotificationBell.razor`** — an interactive component that: resolves the caller's member id from
  `CurrentUser` (as `MyNotificationsAppService` does); on init reads `GetUnreadCountAsync()` and
  `Subscribe(memberId, …)`; on signal re-queries the unread count and `StateHasChanged()`; implements
  `IDisposable` to unsubscribe. Renders a bell with the unread count (0 ⇒ no badge), linking to
  `/notifications/my`.
- **`NotificationBellToolbarContributor : IToolbarContributor`** — adds the bell to
  `StandardToolbars.Main` so it is visible from every page (FR-003, US2). Registered in
  `NotificationsBlazorModule.ConfigureServices`:

```csharp
Configure<AbpToolbarOptions>(options =>
    options.Contributors.Add(new NotificationBellToolbarContributor()));
```

*(API surface verified present in `Volo.Abp.AspNetCore.Components.Web.Theming.Toolbars`:
`AbpToolbarOptions`, `IToolbarContributor`, `StandardToolbars`. Exact `ToolbarItem`/context member names
to be matched to the 10.5.x signatures at implementation.)*

## 3. Inbox live subscription (`Pages/Notifications/MyNotifications.razor`, CHANGED)

The existing page gains the same subscribe-on-init / re-query-on-signal / dispose-on-teardown pattern so
its list and unread count update live (US1), reusing its existing `ReloadAsync()`.

---

## Boundary assertion

The whole contract's signature is `Guid` + `Func<Task>` + `IDisposable`. No `Domain` type, no other
module's type, and no persisted DTO crosses it — so it cannot couple modules or leak history, and the
"absent import" contract-test style (002/005) holds: a subscriber test references only
`Application.Contracts`.

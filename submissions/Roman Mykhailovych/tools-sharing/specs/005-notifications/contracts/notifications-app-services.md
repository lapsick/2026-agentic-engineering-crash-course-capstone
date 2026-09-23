# Notifications Application Services (Module-Internal, Tier 2)

**Requirements**: FR-012, FR-013, FR-014, FR-015 | **Verified by**: SC-004, SC-005, SC-006
**Defined in**: `ToolShare.Notifications.Application.Contracts`

Consumed only by `ToolShare.Notifications.Blazor`. No other module may reference these types — there is
no known future consumer (contracts/README.md).

---

## `IMyNotificationsAppService`

Self-service, resolves the caller's own notifications from `CurrentUser.Id` — never a parameter,
mirroring `IMyMembershipAppService` (003) and `IMyLendingAppService` (004) exactly, including the
`GetOwnMemberIdOrThrowAsync` pattern (resolve `CurrentUser.Id` → `IMemberStandingAppService
.GetByIdentityUserIdAsync` → `MemberId`).

```csharp
namespace ToolShare.Notifications.Notifications;

public interface IMyNotificationsAppService : IApplicationService
{
    Task<PagedResultDto<NotificationDto>> GetListAsync(GetMyNotificationsInput input);

    Task<int> GetUnreadCountAsync();

    Task MarkReadAsync(Guid id);

    Task MarkAllReadAsync();
}
```

| Operation | Behavior | Rejected when |
|---|---|---|
| `GetListAsync` | Own notifications, most recent first (`CreatedAt desc`), paged (default 20, hard cap 100 — matching Catalog's `ToolInstanceLookupFilterDto` convention) | — |
| `GetUnreadCountAsync` | Count of own notifications with `ReadAt = null` (FR-013) | — |
| `MarkReadAsync` | `Notification.MarkRead(now)` (NOTIF-02) for one of the caller's own notifications | Notification does not exist or belongs to another member — `Volo.Abp.EntityNotFoundException`, never a cross-member leak (FR-014) |
| `MarkAllReadAsync` | `MarkRead(now)` applied to every currently-unread notification owned by the caller (NOTIF-03) | — |

```csharp
public class NotificationDto : EntityDto<Guid>
{
    public NotificationKind Kind { get; set; }
    public string DisplayText { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class GetMyNotificationsInput : PagedResultRequestDto
{
    // MaxResultCount hard-capped at 100 by the implementation, mirroring
    // ToolInstanceLookupFilterDto's own convention (002).
}
```

`NotificationDto` deliberately omits `DeliveryRecords` — a member's own inbox shows *what happened*, not
*how it was delivered to them*; the latter is an audit concern, not a self-service one (see below).

---

## `INotificationAuditAppService`

Administrator-only (FR-015's "was this member notified, and how" requirement; SC-006). Not a
self-service surface — takes an explicit id, the one exception in this module to the "never a
parameter, always `CurrentUser`" self-service pattern, exactly as `IMemberAppService` (003) is the
Administrator-facing counterpart to `IMyMembershipAppService`.

```csharp
namespace ToolShare.Notifications.Notifications;

public interface INotificationAuditAppService : IApplicationService
{
    Task<NotificationAuditDto> GetAsync(Guid id);
}

public class NotificationAuditDto : NotificationDto
{
    public Guid MemberId { get; set; }
    public List<NotificationDeliveryRecordDto> DeliveryRecords { get; set; } = new();
}

public class NotificationDeliveryRecordDto
{
    public DeliveryChannel Channel { get; set; }
    public DeliveryStatus Status { get; set; }
    public DateTime AttemptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureDetail { get; set; }
}
```

No list/search endpoint is defined for this feature — an Administrator reaches a specific notification's
audit trail by id (e.g. from a member's own report of "I didn't get an email"), matching the minimum
SC-006 asks for ("for any notification, an Administrator can determine…") without building a full audit
browsing UI the spec does not require.

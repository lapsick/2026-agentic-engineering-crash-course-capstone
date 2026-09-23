# Notifications Permission Boundary

**Requirements**: FR-014, FR-015 | **Verified by**: SC-004, SC-006
**Defined in**: `ToolShare.Notifications.Application.Contracts` (`NotificationsPermissions`,
`NotificationsPermissionDefinitionProvider`)

Notifications introduces **no new enrolment-gate mechanism** — 003's
`MembershipMethodInvocationAuthorizationService` already refuses every application-service call,
across every module, unless the caller is an enrolled, Active member. This document covers only the one
permission this feature adds on top of that gate.

---

## Permission tree

```text
Notifications                (group)
└── Notifications.Audit      view any member's notification + delivery history
```

```csharp
public static class NotificationsPermissions
{
    public const string GroupName = "Notifications";

    public const string Audit = GroupName + ".Audit";
}
```

**Viewing and marking read one's own notifications carries no permission constant** — like every prior
module's self-service surface (Catalog browsing, Membership's `IMyMembershipAppService`, Lending's
`IMyLendingAppService`), it is gated by the enrolment gate alone. Requiring a permission for a member to
see their own inbox would make every freshly enrolled member unable to see why they were notified until
an Administrator granted it — the same reasoning every prior module used.

## Operation-to-permission map

| Operation | Requirement |
|---|---|
| List own notifications, unread count, mark own read/all-read | `[Authorize]` — any active member, self only (FR-012, FR-013, `IMyNotificationsAppService`) |
| View **any** notification's full audit trail (`INotificationAuditAppService.GetAsync`) | `Notifications.Audit` |

### Self-only access (FR-014)

`IMyNotificationsAppService` takes no member id on any method — it resolves the caller's own
notifications from `CurrentUser.Id`, exactly as `IMyLendingAppService`/`IMyMembershipAppService` do. A
member has no operation available anywhere in this module's Tier 2 surface that accepts another
member's id; the only id-taking method (`INotificationAuditAppService.GetAsync`) is gated behind
`Notifications.Audit`, which no `Member`-role user holds.

## Roles and grants

| ABP role | Grants |
|---|---|
| `Member` | None — the inbox is enrolment-gated only |
| `Librarian` | None — this feature defines no Librarian-specific capability (notifications are either "mine" or an Administrator's audit concern; a Librarian has no standing need to read another member's notification history) |
| `Administrator` | `Notifications.Audit` |

Seeding lives in the **host** (`ToolShare.Application/Identity/`), extending
`MembershipRoleDataSeedContributor` (Administrator's grants) exactly as 003 and 004 extended 002's —
a business module must not depend on the Identity module, so granting permissions stays a host-only
concern (004 research R11).

## Verification

| Criterion | Test |
|---|---|
| SC-004 (cross-member leak) | A member's `MarkReadAsync`/list call against another member's notification id is refused or returns nothing — reusing 003's/004's own self-only test pattern |
| SC-006 (Administrator can audit any notification) | `INotificationAuditAppService.GetAsync` executed as `Administrator` returns full delivery history; executed as `Member`/`Librarian` is denied |
| Non-member/inactive blocked | Every Notifications operation executed as (a) an authenticated non-member and (b) a deactivated member, asserting refusal in both cases — reusing 003's `EnrolmentGateTests` pattern |

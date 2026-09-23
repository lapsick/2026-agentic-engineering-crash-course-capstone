# Membership Extension: `Email` on the Standing Lookup

**Requirements**: FR-008 | **Verified by**: SC-003
**Research**: [research.md](../research.md) R1

This is new **Membership** surface this feature adds — the second time a later feature extends an
already-shipped module's published boundary (004's Catalog extension was the first). Unlike that one,
this is not a new capability, only one new field on an existing, already-published DTO. It ships in
`ToolShare.Membership.Application.Contracts`/`ToolShare.Membership.Application`, **not** in any
`ToolShare.Notifications.*` project, and belongs in Membership's own
[contracts documentation](../../003-membership-rules/contracts/) once implemented, per 003's own
precedent of amending 002's docs rather than a later feature silently redefining them.

---

## `MemberStandingDto` gains `Email`

```csharp
namespace ToolShare.Membership.Members;

public class MemberStandingDto
{
    public bool IsEnrolled { get; set; }
    public Guid? MemberId { get; set; }
    public Guid? IdentityUserId { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }   // NEW — this feature
    public bool IsActive { get; set; }
    public MembershipStatus? Status { get; set; }
    public CommunityRole? Role { get; set; }
    public int CurrentRating { get; set; }
    public int EffectiveConcurrentLoanLimit { get; set; }
}
```

Additive only, per the Tier 1 stability rule 002 established — every existing consumer (Lending's
`ReservationAppService`, `MyLendingAppService`, the enrolment gate's authorization service, etc.) keeps
working unchanged; none of them read a field they didn't ask for.

| Method | Populates `Email`? | Why |
|---|---|---|
| `GetAsync(memberId)` | **Yes** | Loads the full `Member` entity (`_memberRepository.FindAsync`), which already carries `Email` (003) |
| `GetByIdsAsync(memberIds)` | **Yes** | Same — loads full `Member` rows |
| `GetByIdentityUserIdAsync(identityUserId)` | **No — unchanged** | Backed by the cached `MemberStandingSnapshot`/`MemberStandingCacheItem` used by the hot, per-request enrolment gate (`MembershipMethodInvocationAuthorizationService`); no current caller of this path needs email, and none of Notifications' calls use it (both source events carry `MemberId`, never only an `IdentityUserId`) |

When `IsEnrolled` is `false` (unknown identity), `Email` is `null`, consistent with every other nullable
field on this DTO.

## Authorization

No change. `MemberStandingAppService` is already `[Authorize]` with no additional permission — every
enrolled, active caller (any module's application service, including Notifications') may resolve any
member's standing, exactly as it could before this field existed. Email is not more sensitive than
`DisplayName`, already exposed here, or `Role`/`CurrentRating`, already exposed here.

## What does not change

- `IMemberStandingAppService`'s method signatures — no new method, no new parameter.
- The cached, identity-keyed hot path (`MemberStandingProvider`, `MemberStandingCacheItem`,
  `MemberStandingSnapshot`) — untouched, so the enrolment gate's cache entry shape and 30-minute sliding
  expiration are unaffected.
- `MemberListItemDto`/`MemberDetailDto` (Tier 2, already expose `Email` to Membership's own Blazor UI) —
  unrelated to this change; this feature does not touch Membership's UI-facing DTOs.
- No new EF Core migration in Membership's own schema — `Member.Email` is an existing column; this is a
  read-projection change only.

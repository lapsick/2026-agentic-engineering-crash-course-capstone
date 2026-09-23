# Membership Internal App Services (Tier 2)

**Requirements**: FR-001…FR-015, FR-021, FR-022, FR-031 | **Verified by**: SC-001, SC-002, SC-006, SC-007, SC-012

Assembly: `ToolShare.Membership.Application.Contracts`
Namespaces: `ToolShare.Membership.Members`, `ToolShare.Membership.CommunityRules`

**Tier 2 — module-internal.** Consumed only by `ToolShare.Membership.Blazor`. No other module may
reference these types; doing so is a Principle II violation even though C# accessibility permits it.
The downstream-facing surface is [membership-public-contracts.md](./membership-public-contracts.md).

Permission requirements for every operation below are in
[membership-permissions.md](./membership-permissions.md); they are not repeated here.

---

## `IMemberAppService` — roster administration (US1)

```csharp
public interface IMemberAppService : IApplicationService
{
    Task<PagedResultDto<MemberListItemDto>> GetListAsync(GetMemberListInput input);
    Task<MemberDetailDto> GetAsync(Guid id);
    Task<List<MemberStandingChangeDto>> GetStandingHistoryAsync(Guid id);

    Task<MemberDetailDto> EnrolAsync(EnrolMemberDto input);
    Task<MemberDetailDto> ChangeRoleAsync(Guid id, ChangeMemberRoleDto input);
    Task<MemberDetailDto> DeactivateAsync(Guid id, DeactivateMemberDto input);
    Task<MemberDetailDto> ReactivateAsync(Guid id, ReactivateMemberDto input);

    Task<MemberDetailDto> AdjustRatingAsync(Guid id, AdjustMemberRatingDto input);
}
```

### Input DTOs

```csharp
public class EnrolMemberDto
{
    [Required, StringLength(128, MinimumLength = 2)] public string DisplayName { get; set; } = default!;
    [Required, EmailAddress, StringLength(256)]      public string Email { get; set; } = default!;
    [Required, StringLength(128, MinimumLength = 6)] public string InitialPassword { get; set; } = default!;
    public CommunityRole Role { get; set; } = CommunityRole.Member;
}

public class ChangeMemberRoleDto
{
    public CommunityRole Role { get; set; }
    [Required] public string ConcurrencyStamp { get; set; } = default!;
}

public class DeactivateMemberDto
{
    [Required, StringLength(512)] public string Reason { get; set; } = default!;
    [Required] public string ConcurrencyStamp { get; set; } = default!;
}

public class ReactivateMemberDto
{
    [Required] public string ConcurrencyStamp { get; set; } = default!;
}

public class AdjustMemberRatingDto
{
    [Range(-100, 100)]            public int Points { get; set; }
    [Required, StringLength(512)] public string Reason { get; set; } = default!;   // FR-021
    [Required] public string ConcurrencyStamp { get; set; } = default!;
}

public class GetMemberListInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }              // name or email, case-insensitive contains
    public MembershipStatus? Status { get; set; }
    public CommunityRole? Role { get; set; }
}
```

`EnrolAsync` accepts an optional `Role` so an Administrator can enrol a Librarian in one action
(SC-001's under-two-minutes journey). The member record is still *created* as `Member` per FR-003 and
then immediately transitioned, so the standing history shows both the `Enrolled` and the
`RoleChanged` entry — history never skips a step.

### Behavior notes

| Operation | Notes |
|---|---|
| `EnrolAsync` | Calls `IMemberIdentityProvisioner.CreateAsync` then creates the aggregate, in **one** unit of work / one database transaction (research R2). Identity-store validation failures (duplicate email, weak password) surface unwrapped so the message names the real conflict (FR-002). |
| `ChangeRoleAsync` | Updates the aggregate **and** calls `IMemberIdentityProvisioner.SetRoleAsync` so the ABP role set matches. Guarded by `MR-10` (last Administrator). |
| `DeactivateAsync` / `ReactivateAsync` | Never touches the identity account (Q2 decision). Guarded by `MR-10`. Reactivation leaves `CurrentRating` untouched (FR-005). |
| `AdjustRatingAsync` | The only path that creates a `ManualAdjustment` entry; `Reason` is mandatory. Clamped exactly like automatic outcomes (`MR-06`). |
| `GetListAsync` | Default `MaxResultCount` 10, hard cap 100. `Sorting` accepts `DisplayName`, `Email`, `Status`, `Role`, `CurrentRating`, `EnrolledAt`; default `DisplayName ASC`. An unrecognized sort field falls back to the default rather than throwing — same rule as Catalog's lookup filter. |
| All mutating operations | Require `ConcurrencyStamp` and throw `AbpDbConcurrencyException` on a stale value (FR-031), matching the behaviour 002 proved in `ConcurrentEditTests`. |

### Output DTOs

```csharp
public class MemberListItemDto : EntityDto<Guid>
{
    public string DisplayName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public MembershipStatus Status { get; set; }
    public CommunityRole Role { get; set; }
    public int CurrentRating { get; set; }
    public DateTime EnrolledAt { get; set; }
}

public class MemberDetailDto : MemberListItemDto
{
    public Guid IdentityUserId { get; set; }
    public int EffectiveConcurrentLoanLimit { get; set; }
    public DateTime? StatusChangedAt { get; set; }
    public string? StatusChangeReason { get; set; }
    public string ConcurrencyStamp { get; set; } = default!;
}

public class MemberStandingChangeDto : EntityDto<Guid>
{
    public MemberStandingChangeKind Kind { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByDisplayName { get; set; }
    public string? Reason { get; set; }

    public MembershipStatus? PreviousStatus { get; set; }
    public MembershipStatus? NewStatus { get; set; }
    public CommunityRole? PreviousRole { get; set; }
    public CommunityRole? NewRole { get; set; }

    public ReliabilityOutcomeType? OutcomeType { get; set; }
    public int? RawPoints { get; set; }
    public int? EffectivePoints { get; set; }
    public int? ResultingRating { get; set; }
    public Guid? OccurrenceId { get; set; }
}
```

`ChangedByDisplayName` is denormalized into the history DTO deliberately: it saves the timeline UI a
lookup per row and carries no coupling because it is a value, not a reference — the same reasoning
that put `ToolName` on `ToolInstanceLookupDto` in 002. It is resolved from the Membership roster
(the actor is always a member), never from the identity store.

---

## `IMyMembershipAppService` — self-service (US3)

```csharp
public interface IMyMembershipAppService : IApplicationService
{
    Task<MyMembershipDto> GetAsync();
    Task<List<MemberStandingChangeDto>> GetStandingHistoryAsync();
}

public class MyMembershipDto
{
    public Guid MemberId { get; set; }
    public string DisplayName { get; set; } = default!;
    public MembershipStatus Status { get; set; }
    public CommunityRole Role { get; set; }
    public int CurrentRating { get; set; }
    public int EffectiveConcurrentLoanLimit { get; set; }
    public DateTime EnrolledAt { get; set; }
}
```

**Takes no member id — by design.** The caller's member record is resolved from
`CurrentUser.GetId()`, so "read someone else's standing" is not a check that can be got wrong; it is
an operation the interface does not offer (FR-022, SC-004). See
[membership-permissions.md](./membership-permissions.md#self-only-access-fr-022-sc-004).

There is no mutating operation here at all: nothing lets a member change their own status, role, or
rating (US3 scenario 5). `MyMembershipDto` carries no `ConcurrencyStamp` for the same reason.

SC-012 ("a member can determine why their rating has its current value") is met by
`GetStandingHistoryAsync`: every `RatingOutcome` entry reports `EffectivePoints` and
`ResultingRating`, so the difference from 100 is fully accounted for row by row.

---

## `ICommunityRulesAppService` — rules administration (US2)

```csharp
public interface ICommunityRulesAppService : IApplicationService
{
    Task<CommunityRulesDetailDto> GetAsync();
    Task<CommunityRulesDetailDto> UpdateAsync(UpdateCommunityRulesDto input);
}

public class CommunityRulesDetailDto : CommunityRulesDto        // adds edit metadata
{
    public string? LastChangedByDisplayName { get; set; }
    public string ConcurrencyStamp { get; set; } = default!;
}

public class UpdateCommunityRulesDto
{
    [Range(1, 365)] public int MaxLoanTermDays { get; set; }
    [Range(1, 100)] public int ConcurrentLoanLimit { get; set; }
    [Range(0, 100)] public int LowRatingThreshold { get; set; }
    [Range(1, 100)] public int ReducedConcurrentLoanLimit { get; set; }
    [Range(0, 100)] public int OverduePenaltyPoints { get; set; }
    [Range(0, 100)] public int DamagePenaltyPoints { get; set; }
    [Range(0, 100)] public int CleanReturnRewardPoints { get; set; }
    [Range(1, 8760)] public int WaitlistOfferWindowHours { get; set; }
    [Range(1, 365)] public int ReminderLeadTimeDays { get; set; }

    [Required] public string ConcurrencyStamp { get; set; } = default!;
}
```

Data annotations catch the per-field ranges. The **cross-field** rule
(`ReducedConcurrentLoanLimit ≤ ConcurrentLoanLimit`) is enforced in the domain by
`CommunityRules.Update` and surfaces as `Membership:InvalidRule` with a `rule` data key naming the
offender (FR-014, `CRR-01`) — it lives in the domain rather than in a validator because it is a rule
about the community, not about the request shape, and must hold on every path.

`UpdateAsync` requires `ConcurrencyStamp`; two Administrators saving concurrently means the second
gets `AbpDbConcurrencyException` (FR-031, US2 scenario 7).

The DTO returned to the UI extends the public `CommunityRulesDto` rather than duplicating its
fields, so a rule added later cannot be added to one and forgotten on the other.

---

## Error codes (`ToolShare.Membership.Domain.Shared`)

| Code | Raised when |
|---|---|
| `Membership:DisplayNameRequired` | `MR-01` |
| `Membership:IdentityAlreadyEnrolled` | `MR-02` — identity id already has a member record (FR-002) |
| `Membership:AlreadyDeactivated` / `Membership:AlreadyActive` | `MR-04` |
| `Membership:RoleUnchanged` | `MR-05` |
| `Membership:LastAdministrator` | `MR-10` (FR-007, SC-006) |
| `Membership:NotAnEnrolledMember` | Outcome reported for a non-member (FR-027); also the gate's refusal reason |
| `Membership:MembershipInactive` | The enrolment gate refusing a deactivated member (FR-006) |
| `Membership:ManualAdjustmentNotReportable` | `ManualAdjustment` submitted through the public reporting contract (FR-021) |
| `Membership:InvalidRule` | `CRR-01`, with a `rule` data key naming the field (FR-014) |
| `Membership:AdjustmentReasonRequired` | Manual adjustment without a reason (FR-021) |

All are localized through `MembershipResource`, following the pattern Catalog uses for `Catalog:*`.

# Membership Public Contracts (Tier 1)

**Requirements**: FR-024, FR-025, FR-026, FR-027, FR-029 | **Verified by**: SC-011

Assembly: `ToolShare.Membership.Application.Contracts`
Namespaces: `ToolShare.Membership.Members`, `ToolShare.Membership.CommunityRules`
Enums from: `ToolShare.Membership` (`ToolShare.Membership.Domain.Shared`)

This is the **entire** surface a downstream module (Lending, Notifications — features 004+) may take
a compile-time dependency on. Everything else in Membership is internal.

---

## `IMemberStandingAppService`

```csharp
namespace ToolShare.Membership.Members;

/// <summary>
/// Read-only standing lookup for other modules. Consumers depend on this
/// interface and its DTOs only — never on Membership's Domain or
/// EntityFrameworkCore layer.
/// </summary>
public interface IMemberStandingAppService : IApplicationService
{
    Task<MemberStandingDto> GetByIdentityUserIdAsync(Guid identityUserId);

    Task<MemberStandingDto> GetAsync(Guid memberId);

    Task<List<MemberStandingDto>> GetByIdsAsync(IEnumerable<Guid> memberIds);
}
```

| Operation | Behavior | Authorization |
|---|---|---|
| `GetByIdentityUserIdAsync(id)` | Always returns a DTO — never `null`, never throws for "not found". An unknown identity yields `IsEnrolled = false` (FR-025). Backed by the cached, identity-keyed snapshot the enrolment gate's hot path uses; `Email` is **not** populated on this path (005-notifications, [contracts/membership-extension.md](../../005-notifications/contracts/membership-extension.md)). | Active member |
| `GetAsync(memberId)` | Same, keyed by member id. An unknown member id yields `IsEnrolled = false`. `Email` **is** populated. | Active member |
| `GetByIdsAsync(ids)` | Batch form. Unknown ids are **omitted** (no exception, no `null` entries), so the result may be shorter than the input. Order unspecified. Empty input → empty list. | Active member |

**Contract guarantees**
- No method throws for absence; absence is expressed as `IsEnrolled = false` or an omitted element.
- No method mutates state; all are safe from a read-only unit of work.
- Deactivated members are **returned**, with `IsActive = false` — a consumer must be able to resolve
  a historical reference, the same reason Catalog returns retired instances.
- Implemented by `MemberStandingAppService` in `ToolShare.Membership.Application` — consumers
  resolve the **interface** and never name the implementation.

---

## `MemberStandingDto`

```csharp
public class MemberStandingDto
{
    public bool IsEnrolled { get; set; }
    public Guid? MemberId { get; set; }
    public Guid? IdentityUserId { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }   // NEW — 005-notifications, additive

    public bool IsActive { get; set; }
    public MembershipStatus? Status { get; set; }
    public CommunityRole? Role { get; set; }

    public int CurrentRating { get; set; }
    public int EffectiveConcurrentLoanLimit { get; set; }
}
```

### The two-flag design (FR-025)

```text
not enrolled          → IsEnrolled = false, IsActive = false, MemberId = null
enrolled, deactivated → IsEnrolled = true,  IsActive = false, MemberId = <id>
enrolled, active      → IsEnrolled = true,  IsActive = true,  MemberId = <id>
```

Two independent booleans rather than one tri-state enum, so the common consumer check stays a
single condition (`if (!standing.IsActive) reject;`) while a module that must distinguish the two
rejection reasons — to say "you are not a member of this community" versus "your membership is
inactive" — still can. When `IsEnrolled` is false every nullable field is `null` and both numeric
fields are `0`.

### `EffectiveConcurrentLoanLimit` semantics

```text
EffectiveConcurrentLoanLimit  =  CurrentRating >= rules.LowRatingThreshold
                                    ? rules.ConcurrentLoanLimit
                                    : rules.ReducedConcurrentLoanLimit
```

Computed at read time from the current rules (`MR-08` in [../data-model.md](../data-model.md)), so a
rules change takes effect for every member on the next call with no data migration.

This is **Membership's** notion of allowance: how many concurrent loans the community's rules permit
this person. It deliberately says nothing about how many loans they *currently hold*, or whether any
is overdue — Membership does not know about loans.

> **Forward-compatibility note for Lending (004+).** Lending must compute borrowability as
> `standing.IsActive && lending.ActiveLoanCount(memberId) < standing.EffectiveConcurrentLoanLimit && !lending.HasOverdue(memberId)`.
> The overdue rule (product spec FR-010) is Lending's, not Membership's. The meaning of these fields
> will not change when Lending ships — that is precisely what makes this contract stable (FR-029),
> and it is the same split Catalog made with `IsAvailable`.

---

## `ICommunityRulesLookupAppService`

```csharp
namespace ToolShare.Membership.CommunityRules;

public interface ICommunityRulesLookupAppService : IApplicationService
{
    Task<CommunityRulesDto> GetAsync();
}
```

```csharp
public class CommunityRulesDto
{
    public int MaxLoanTermDays { get; set; }
    public int ConcurrentLoanLimit { get; set; }
    public int LowRatingThreshold { get; set; }
    public int ReducedConcurrentLoanLimit { get; set; }
    public int OverduePenaltyPoints { get; set; }
    public int DamagePenaltyPoints { get; set; }
    public int CleanReturnRewardPoints { get; set; }
    public int WaitlistOfferWindowHours { get; set; }
    public int ReminderLeadTimeDays { get; set; }

    public DateTime? LastChangedAt { get; set; }
    public Guid? LastChangedByUserId { get; set; }
}
```

| Operation | Behavior | Authorization |
|---|---|---|
| `GetAsync()` | Always returns the single rules row; never `null` — the seeder guarantees it exists (`CRR-02`). | Active member (FR-013) |

Read-only by design. Mutation lives on the internal `ICommunityRulesAppService`
([membership-app-services.md](./membership-app-services.md)) and is Administrator-only — a downstream
module must never be able to rewrite the community's rules.

---

## `IReliabilityReportingAppService`

The one **inbound** operation on the public boundary: how Lending tells Membership that something
rating-affecting happened.

```csharp
namespace ToolShare.Membership.Members;

public interface IReliabilityReportingAppService : IApplicationService
{
    Task<ReliabilityReportResultDto> ReportAsync(ReportReliabilityOutcomeDto input);
}

public class ReportReliabilityOutcomeDto
{
    public Guid MemberId { get; set; }
    public ReliabilityOutcomeType OutcomeType { get; set; }

    /// <summary>
    /// Identifier of the thing in the calling module that caused this outcome
    /// (a loan, a return). Required — it is the idempotency key.
    /// </summary>
    public Guid OccurrenceId { get; set; }

    public string? Note { get; set; }
}

public class ReliabilityReportResultDto
{
    public bool Applied { get; set; }
    public bool AlreadyRecorded { get; set; }
    public int RawPoints { get; set; }
    public int EffectivePoints { get; set; }
    public int ResultingRating { get; set; }
}
```

| Case | Result | Requirement |
|---|---|---|
| Outcome applied | `Applied = true`, `AlreadyRecorded = false`, points and resulting rating populated | FR-027 |
| Same `(OccurrenceId, OutcomeType)` reported again | `Applied = false`, `AlreadyRecorded = true`, `ResultingRating` = the member's current rating. **No exception** — a retrying caller must be able to treat this as success | FR-019, SC-009 |
| Member id unknown, or the person is not enrolled | throws `BusinessException("Membership:NotAnEnrolledMember")` | FR-027 |
| `OutcomeType = ManualAdjustment` | throws `BusinessException("Membership:ManualAdjustmentNotReportable")` — manual corrections are Administrator-only and go through the internal service | FR-021 |
| Concurrent report for the same member | Serialized and retried internally; the caller sees one of the rows above, **never** a contention failure | FR-032, SC-009a |

**Deactivated members accept outcomes.** A member deactivated while still holding a tool must be
able to return it, and that return legitimately moves their rating. Deactivation blocks access, not
bookkeeping (see the state-transition note in [../data-model.md](../data-model.md)).

`RawPoints` versus `EffectivePoints`: the first is what the rules called for, the second is what
survived clamping to 0–100. They differ exactly when the rating hit a bound — which is how a caller
can tell "penalty applied" from "penalty absorbed because they were already at 0".

**Authorization**: `Membership.Reliability.Report`, granted to the `Librarian` and `Administrator`
roles. Lending's app services run under the acting Librarian's principal, so no service account is
needed.

---

## `IMemberIdentityProvisioner` (inbound port — host-implemented)

```csharp
namespace ToolShare.Membership.Members;

/// <summary>
/// Port Membership publishes so the HOST can supply identity-account creation.
/// Membership must not reference Volo.Abp.Identity (Principle II, and the rule
/// 002 set with LibrarianRoleDataSeedContributor), so the dependency is inverted:
/// Membership declares, the host implements.
/// Downstream modules must NOT call this — it exists for the host adapter only.
/// </summary>
public interface IMemberIdentityProvisioner
{
    Task<Guid> CreateAsync(string displayName, string email, string initialPassword);

    Task SetRoleAsync(Guid identityUserId, CommunityRole role);

    Task<bool> ExistsAsync(Guid identityUserId);
}
```

| Operation | Behavior |
|---|---|
| `CreateAsync` | Creates the sign-in account, flags it `ShouldChangePasswordOnNextLogin` (FR-001a), returns the new identity user id. Surfaces the identity store's own validation errors (duplicate email/username, weak password) unwrapped, so FR-002's message names the real conflict. |
| `SetRoleAsync` | **Replaces** the user's role set with the single ABP role matching the `CommunityRole` — never merges, so "exactly one role" (FR-004) cannot drift. |
| `ExistsAsync` | Used by the seeder and by the "identity removed underneath a member record" edge case. |

Runs inside the caller's unit of work. Because `ToolShareDbContext` (identity) and
`MembershipDbContext` share the `Default` connection string, enrolment is a single database
transaction: if the `Member` row fails to insert, the identity account is rolled back with it.

---

## Shared enums

Defined in `ToolShare.Membership.Domain.Shared`, reachable by consumers transitively through
`Membership.Application.Contracts`. See [../data-model.md](../data-model.md) for the authoritative
tables.

```csharp
namespace ToolShare.Membership;

public enum MembershipStatus { Active = 0, Deactivated = 1 }

public enum CommunityRole { Member = 0, Librarian = 1, Administrator = 2 }

public enum ReliabilityOutcomeType { OverdueReturn = 0, DamagedReturn = 1, CleanReturn = 2, ManualAdjustment = 3 }

public enum MemberStandingChangeKind { Enrolled = 0, StatusChanged = 1, RoleChanged = 2, RatingOutcome = 3 }
```

`CommunityRole`'s **numeric order is part of the contract** — capability checks are written
`role >= CommunityRole.Librarian`. Consumers must still use a `default` arm when switching, since
new values may be appended.

---

## Consumption example (illustrative — the future Lending module)

```csharp
public class LoanAppService : ApplicationService
{
    private readonly IMemberStandingAppService _standing;          // Membership.Application.Contracts
    private readonly IReliabilityReportingAppService _reliability; // Membership.Application.Contracts
    private readonly IToolInstanceLookupAppService _toolInstances; // Catalog.Application.Contracts

    public async Task RequestAsync(Guid toolInstanceId)
    {
        var standing = await _standing.GetByIdentityUserIdAsync(CurrentUser.GetId());

        if (!standing.IsEnrolled) throw new BusinessException("Lending:NotAMember");
        if (!standing.IsActive)   throw new BusinessException("Lending:MembershipInactive");

        // Membership supplies the allowance; Lending supplies the count. Neither knows the other's data.
        var held = await _loanRepository.CountActiveAsync(standing.MemberId!.Value);
        if (held >= standing.EffectiveConcurrentLoanLimit)
        {
            throw new BusinessException("Lending:ConcurrentLoanLimitReached");
        }

        var instance = await _toolInstances.FindAsync(toolInstanceId)
            ?? throw new BusinessException("Lending:UnknownToolInstance");
        if (!instance.IsAvailable) throw new BusinessException("Lending:ToolInstanceNotAvailable");

        // Store only identifiers — no FK across schemas (Constitution III).
        await _loanRepository.InsertAsync(new Loan(GuidGenerator.Create(), toolInstanceId, standing.MemberId.Value));
    }

    public async Task CloseAsync(Guid loanId, bool wasLate, bool wasDamaged)
    {
        var loan = await _loanRepository.GetAsync(loanId);

        // OccurrenceId = the loan. Reporting twice is safe (AlreadyRecorded), and one loan may
        // legitimately produce two outcomes — which is why the idempotency key is composite.
        if (wasLate)
        {
            await _reliability.ReportAsync(new ReportReliabilityOutcomeDto
            {
                MemberId = loan.MemberId,
                OutcomeType = ReliabilityOutcomeType.OverdueReturn,
                OccurrenceId = loan.Id
            });
        }
        if (wasDamaged)
        {
            await _reliability.ReportAsync(new ReportReliabilityOutcomeDto
            {
                MemberId = loan.MemberId,
                OutcomeType = ReliabilityOutcomeType.DamagedReturn,
                OccurrenceId = loan.Id
            });
        }
    }
}
```

Note what is absent: no `Member` entity, no `MembershipDbContext`, no repository, no cross-schema
foreign key, and no knowledge of how the rating is calculated.

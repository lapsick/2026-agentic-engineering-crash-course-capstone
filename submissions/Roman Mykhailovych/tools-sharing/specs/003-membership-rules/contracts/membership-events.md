# Membership Integration Events (Tier 1)

**Requirements**: FR-028, FR-017a | **Verified by**: SC-010, SC-011
**Channel**: `ILocalEventBus` (Constitution II) | **Assembly**: `ToolShare.Membership.Domain.Shared`

The ETO lives in `Domain.Shared`, not `Application.Contracts`, for the same reason 002 discovered
mid-implementation: `Domain` raises it via `AddLocalEvent` and standard ABP layering does not let
`Domain` reference `Application.Contracts`. It is still part of the Tier 1 public boundary —
`Application.Contracts` re-exposes it transitively, exactly like the enums. Recording it here up
front means 003 does not have to rediscover the correction.

Membership publishes exactly **one** event, covering all four kinds of standing change.

---

## `MemberStandingChangedEto`

```csharp
namespace ToolShare.Membership.Members;

/// <summary>
/// Raised whenever a member's standing changes: enrolment, status change,
/// role change, or a rating outcome.
/// </summary>
[Serializable]
public class MemberStandingChangedEto
{
    public Guid MemberId { get; set; }
    public Guid IdentityUserId { get; set; }

    public MemberStandingChangeKind Kind { get; set; }

    public MembershipStatus? PreviousStatus { get; set; }
    public MembershipStatus NewStatus { get; set; }

    public CommunityRole? PreviousRole { get; set; }
    public CommunityRole NewRole { get; set; }

    public int? PreviousRating { get; set; }
    public int NewRating { get; set; }

    /// <summary>
    /// True when this change moved the rating across CommunityRules.LowRatingThreshold
    /// in either direction — the signal that a member's borrowing allowance changed.
    /// </summary>
    public bool CrossedLowRatingThreshold { get; set; }

    public ReliabilityOutcomeType? OutcomeType { get; set; }
    public Guid? OccurrenceId { get; set; }

    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }          // UTC
    public Guid? ChangedByUserId { get; set; }
}
```

### When it is raised

| Trigger | `Kind` | `Previous*` | `New*` | `Reason` |
|---|---|---|---|---|
| Member enrolled | `Enrolled` | all `null` | `Active`, `Member`, rating `100` | `null` |
| Deactivated / reactivated | `StatusChanged` | previous status, current role, current rating | new status, unchanged role/rating | **required** |
| Role changed | `RoleChanged` | current status, previous role, current rating | unchanged status, new role, unchanged rating | optional |
| Rating outcome applied | `RatingOutcome` | current status/role, previous rating | unchanged status/role, new rating | optional note; **required** for `ManualAdjustment` |

`NewStatus`, `NewRole` and `NewRating` are **always** populated regardless of kind — they describe
the member's complete standing after the change, so a consumer that only cares about "what is true
now" never has to branch on `Kind`. The nullable `Previous*` fields say what moved.

The state-change dimensions that did not move repeat their current value in both `Previous*` and
`New*` — the same convention as `ToolInstanceStateChangedEto` (`HR-04` in 002's data model).

### `CrossedLowRatingThreshold`

Computed by the aggregate at the moment of the transition:

```text
CrossedLowRatingThreshold  ⇔  (previousRating >= threshold) != (newRating >= threshold)
```

It is precomputed rather than left to consumers because the threshold lives in `CommunityRules`,
which a consumer would otherwise have to fetch and compare on **every** event just to discover the
event was uninteresting. FR-028 names threshold crossing as one of the things the event must
announce; this field is that announcement.

> A rules change that moves the threshold does **not** raise this event for every affected member —
> no member's standing changed, only the rule did. Consumers that cache allowances must therefore
> also treat a rules change as invalidating; `ICommunityRulesLookupAppService.GetAsync` exposes
> `LastChangedAt` for exactly that purpose.

### Publication semantics

The event is added on the aggregate root, not published imperatively:

```csharp
// inside Member.Deactivate(...) — same private helper that appends the history entry
AddLocalEvent(new MemberStandingChangedEto { /* … */ });
```

This gives three guarantees consumers may rely on, identical to Catalog's:

1. **Transactional** — ABP's unit of work dispatches added local events only after `SaveChanges`
   succeeds. A handler never observes a change that was rolled back.
2. **In-process and synchronous** — handlers run in the same process and request scope. A handler
   that throws surfaces to the caller, so handlers must be fast and must not do long-running work.
3. **At-most-once per transition** — no retry, no outbox. Work that must survive a crash belongs in
   ABP Background Jobs, not directly in a local handler.

`ChangedAt` is the UTC timestamp from ABP's `IClock` at the moment of the domain transition — not
dispatch time.

**One transition ⇒ exactly one history entry ⇒ exactly one event** (FR-017a, `HR-04`). Both are
written by the same private helper on `Member`, so they cannot diverge. An integration test asserts
the counts match after a mixed sequence of transitions.

### Subscription

```csharp
public class MemberStandingChangedHandler
    : ILocalEventHandler<MemberStandingChangedEto>, ITransientDependency
{
    public async Task HandleEventAsync(MemberStandingChangedEto eventData)
    {
        // React using identifiers only — never load Membership entities directly.
        if (eventData.Kind == MemberStandingChangeKind.StatusChanged &&
            eventData.NewStatus == MembershipStatus.Deactivated)
        {
            await _loanRepository.FlagOutstandingForFollowUpAsync(eventData.MemberId);
        }

        if (eventData.CrossedLowRatingThreshold)
        {
            await _allowanceCache.InvalidateAsync(eventData.MemberId);
        }
    }
}
```

Subscribing requires a reference to `ToolShare.Membership.Application.Contracts` and nothing else.
Handlers are discovered by ABP's conventional registration, so **Membership never learns who its
consumers are** — the property that keeps FR-029 true.

### Membership's own internal handler

Membership itself subscribes to this event to evict the standing cache that backs the enrolment gate
(research [R3](../research.md#r3--enforcing-the-enrolment-gate-fr-006-fr-006a)). Because the handler
runs in the same unit of work that recorded the change, a deactivation is refused on the member's
very next action — which is what FR-006 requires and what SC-005 tests.

### Compatibility rules

- Additive changes only: new **optional** properties may be added; existing ones are never renamed,
  removed or retyped.
- New `MemberStandingChangeKind` and `ReliabilityOutcomeType` values will flow through this same
  event at new numeric values. Handlers must use a `default` arm rather than assuming the current
  value set.
- `CommunityRole` may gain values only in positions that preserve its numeric ordering (see
  [README.md](./README.md)).
- The event carries **identifiers and values, never entity references** — required by Constitution
  III, since consumers live in other schemas.

### Test hooks

| Criterion | Test |
|---|---|
| SC-010 (one entry, one event) | Perform enrol → deactivate → reactivate → role change → two rating outcomes; assert 6 history entries and 6 received events with matching `Kind`/`Previous*`/`New*` pairs |
| SC-011 (boundary) | A probe handler registered in the test module receives the event while the test file imports only `ToolShare.Membership.Members` |
| FR-028 (threshold crossing) | Drive a member from 100 below the threshold and back; assert `CrossedLowRatingThreshold` is true exactly twice |

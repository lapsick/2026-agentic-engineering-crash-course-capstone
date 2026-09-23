# Membership Consumption

**Requirements**: FR-018, FR-019, FR-020, FR-021, FR-028 | **Verified by**: SC-005, SC-006, SC-007

Exactly which of [003's published contracts](../../003-membership-rules/contracts/membership-public-contracts.md)
Lending depends on, and how each is used. Lending never stores, derives, or caches any of this data in
its own schema (FR-028) — every call below is made live, at the moment an eligibility decision or a
reliability report is needed.

---

## `IMemberStandingAppService` (read)

Called at the start of every reservation attempt (`RES-04`, `RES-05`) and every checkout attempt
(`LOAN-06`'s member-side check):

```text
standing = await memberStandingAppService.GetByIdentityUserIdAsync(currentUser.Id);

if (!standing.IsEnrolled) → refuse (FR-019, mirrors Membership's own "not an enrolled member" answer)
if (!standing.IsActive)   → refuse (consistent with 003's own access rule)
```

`standing.EffectiveConcurrentLoanLimit` is compared against the member's current count of `Active`
reservations plus open loans (`RES-05`); `standing.CurrentRating` itself is never read directly by
Lending — only the derived limit Membership already computed from it, exactly as 003's own spec
anticipated: *"Lending must compute borrowability as `standing.IsActive && lending.ActiveLoanCount(memberId)
< standing.EffectiveConcurrentLoanLimit && !lending.HasOverdue(memberId)`. The overdue rule is Lending's,
not Membership's."*

`lending.HasOverdue(memberId)` is Lending's own `RES-04` check against its own `Loan` rows
(`IsOverdue`) — Membership's standing lookup says nothing about loans, deliberately (003 spec,
Assumptions).

---

## `ICommunityRulesLookupAppService` (read)

Called wherever a rule value is needed:

| Rule | Used for |
|---|---|
| `MaxLoanTermDays` | `RES-01` |
| `WaitlistOfferWindowHours` | `WL-03` |
| `ReminderLeadTimeDays` | `ReturnReminderWorker`'s query window |

`ConcurrentLoanLimit`, `LowRatingThreshold`, `ReducedConcurrentLoanLimit`, `OverduePenaltyPoints`,
`DamagePenaltyPoints`, `CleanReturnRewardPoints` are read by Membership itself when computing
`EffectiveConcurrentLoanLimit` and applying a reported outcome — Lending never reads these values
directly; it only supplies the outcome type and lets Membership apply its own configured point values
(`LOAN-05`), exactly as 003's `ReliabilityPolicy` was designed to keep those values authoritative in
one place.

---

## `IReliabilityReportingAppService` (write)

Called exactly once per applicable outcome when a loan closes (`LOAN-05`):

```csharp
if (loan.IsOverdue)
{
    await reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
    {
        MemberId = loan.MemberId,
        OutcomeType = ReliabilityOutcomeType.OverdueReturn,
        OccurrenceId = loan.Id           // Loan.Id doubles as the occurrence id — research R5
    });
}
if (wasWorsened)
{
    await reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
    {
        MemberId = loan.MemberId,
        OutcomeType = ReliabilityOutcomeType.DamagedReturn,
        OccurrenceId = loan.Id
    });
}
if (!loan.IsOverdue && !wasWorsened)
{
    await reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
    {
        MemberId = loan.MemberId,
        OutcomeType = ReliabilityOutcomeType.CleanReturn,
        OccurrenceId = loan.Id
    });
}
```

No retry wrapper is added around these calls (research R8) — Membership's own implementation already
absorbs concurrent-contention retries and never surfaces `AbpDbConcurrencyException` to its caller.
The two genuine rejections Lending's call site must be prepared for are
`Membership:NotAnEnrolledMember` (structurally should never occur here, since `RES-04`/checkout already
verified enrolment moments earlier — treated as a defensive `AbpException`, not a user-facing
business rejection) and `Membership:ManualAdjustmentNotReportable` (unreachable — Lending never
reports `ManualAdjustment`).

`Loan.ReliabilityReportedAt` (data-model.md) is set once every applicable report above has succeeded,
so a retried close operation (e.g., after an application crash mid-close) can detect it has already
reported and skip re-reporting — this is Lending's *own* idempotency safeguard, layered on top of, not
instead of, Membership's own `(OccurrenceId, OutcomeType)` guarantee.

---

## `MemberStandingChangedEto` (subscribed?)

**Not subscribed by this feature.** Lending has no reaction to a member's standing changing — it
reads standing fresh at the moment of each decision (above) rather than maintaining a cached
projection of it. A future feature that needs to react to standing changes (e.g., Notifications
announcing "your rating dropped") subscribes independently; this feature does not need to.

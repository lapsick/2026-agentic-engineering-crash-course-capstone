# Lending Events

**Requirements**: FR-022, FR-023 | **Verified by**: SC-008

Assembly: `ToolShare.Lending.Domain.Shared`
Namespace: `ToolShare.Lending.Loans`

Lending's only outward-facing Tier 1 surface (see [README.md](./README.md)) — everything a future
Notifications feature needs to know that a loan-related deadline is relevant to a member, without
that feature ever importing `ToolShare.Lending.Domain` or `…EntityFrameworkCore`.

---

## `LendingNotificationDueEto`

```csharp
namespace ToolShare.Lending.Loans;

[Serializable]
public class LendingNotificationDueEto
{
    public Guid LoanId { get; set; }
    public Guid MemberId { get; set; }
    public Guid ToolInstanceId { get; set; }
    public LendingNotificationKind Kind { get; set; }
    public DateOnly PlannedReturnDate { get; set; }
    public DateTime RaisedAt { get; set; }
}

public enum LendingNotificationKind
{
    ReturnReminder = 0,
    Overdue = 1
}
```

| Field | Meaning |
|---|---|
| `LoanId` | The loan this notification concerns — a future Notifications feature can look up further detail (who, what) through Membership's and Catalog's own public contracts if it needs display text, exactly as Lending itself does |
| `Kind` | `ReturnReminder` — raised once, when `rules.ReminderLeadTimeDays` before `PlannedReturnDate` is reached. `Overdue` — raised once, when `PlannedReturnDate` passes with no return recorded |
| `RaisedAt` | When the generating background worker (research R5) observed the condition, not necessarily the exact deadline moment |

**Guarantees**

- Raised **at most once per loan per kind** — `Loan.ReminderSentAt`/`OverdueNoticeSentAt` (data-model.md,
  `LOAN-07`) make the generating workers idempotent; a handler that reacts more than once to the same
  `(LoanId, Kind)` pair indicates a bug in the handler, not a duplicate publication.
- Carries **no message text, no channel, no delivery status** — deliberately. Composing and delivering
  the actual reminder/notice (in-app, email, or otherwise) is out of scope for this feature (spec.md
  Assumptions) and belongs entirely to whatever feature consumes this event.
- New `LendingNotificationKind` values may be appended at higher numeric values (mirroring
  `CommunityRole`'s and `ReliabilityOutcomeType`'s own compatibility rule); a consumer MUST use a
  `default` switch arm.

---

## Why an event and not a published query

Membership's `MemberStandingChangedEto` (003) is the direct precedent: a fact worth knowing about the
instant it becomes true, with no natural "ask me later" query shape (nobody polls "is my loan overdue
yet?" the way they'd poll "what's this member's rating?"). An event lets a future Notifications feature
react at the moment the fact becomes true without Lending needing to know that feature exists yet —
the same reasoning 003 gave for its own standing-changed event.

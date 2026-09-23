using System;

namespace ToolShare.Lending.Loans;

/// <summary>
/// Lending's only outward-facing Tier 1 surface (contracts/lending-events.md):
/// a loan-related deadline is due. Raised by <c>ReturnReminderWorker</c> and
/// <c>OverdueMarkingWorker</c> at most once per <c>(LoanId, Kind)</c> pair
/// (<c>Loan.ReminderSentAt</c>/<c>OverdueNoticeSentAt</c> make the workers
/// idempotent). Carries no message text, channel, or delivery status —
/// composing and delivering the actual notification is out of scope for this
/// feature (spec.md Assumptions) and belongs to whatever feature consumes this
/// event.
/// </summary>
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

/// <summary>
/// New values may be appended at higher numeric values (mirroring
/// <c>CommunityRole</c>'s and <c>ReliabilityOutcomeType</c>'s own compatibility
/// rule); a consumer MUST use a <c>default</c> switch arm.
/// </summary>
public enum LendingNotificationKind
{
    ReturnReminder = 0,
    Overdue = 1
}

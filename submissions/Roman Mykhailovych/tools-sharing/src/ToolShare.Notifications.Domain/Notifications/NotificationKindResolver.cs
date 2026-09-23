using ToolShare.Lending.Loans;
using ToolShare.Membership;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// Pure domain rule (no database, no ABP infrastructure): which of the two
/// source events' kinds warrant a notification, and which
/// <see cref="NotificationKind"/> each becomes (FR-001–FR-004).
/// </summary>
public static class NotificationKindResolver
{
    /// <summary>
    /// FR-001/FR-002: every currently-defined Lending-sourced kind warrants a
    /// notification. <c>null</c> for any value not yet known to this feature —
    /// `LendingNotificationKind`'s own compatibility rule requires every
    /// consumer to use a default switch arm, since new values may be appended
    /// at higher numeric values without notice.
    /// </summary>
    public static NotificationKind? TryMapLendingKind(LendingNotificationKind kind)
    {
        return kind switch
        {
            LendingNotificationKind.ReturnReminder => NotificationKind.LoanReturnReminder,
            LendingNotificationKind.Overdue => NotificationKind.LoanOverdue,
            _ => null
        };
    }

    /// <summary>
    /// FR-003/FR-004: deactivation, reactivation, and role changes always
    /// warrant a notification; a rating outcome warrants one only when it
    /// crosses the low-rating threshold (not every point fluctuation);
    /// enrolment does not (a member isn't signed in yet to see it). <c>null</c>
    /// for any value not yet known to this feature, for the same
    /// forward-compatibility reason as <see cref="TryMapLendingKind"/>.
    /// </summary>
    public static NotificationKind? TryMapStandingChange(
        MemberStandingChangeKind kind,
        MembershipStatus? previousStatus,
        MembershipStatus newStatus,
        bool crossedLowRatingThreshold)
    {
        return kind switch
        {
            MemberStandingChangeKind.StatusChanged => newStatus switch
            {
                MembershipStatus.Deactivated => NotificationKind.StandingDeactivated,
                MembershipStatus.Active => NotificationKind.StandingReactivated,
                _ => null
            },
            MemberStandingChangeKind.RoleChanged => NotificationKind.StandingRoleChanged,
            MemberStandingChangeKind.RatingOutcome => crossedLowRatingThreshold ? NotificationKind.StandingLowRatingCrossed : null,
            _ => null
        };
    }
}

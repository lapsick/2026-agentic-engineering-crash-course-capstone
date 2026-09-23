namespace ToolShare.Notifications;

/// <summary>
/// Internal to Notifications' own operation — not part of any published
/// boundary this feature exposes (data-model.md), unlike Catalog's/
/// Membership's/Lending's frozen public enums. New values may still be
/// appended at higher numeric values as a matter of habit, but no external
/// consumer's compatibility depends on it.
/// </summary>
public enum NotificationKind
{
    /// <summary>From <c>LendingNotificationDueEto</c> (<c>Kind = ReturnReminder</c>).</summary>
    LoanReturnReminder = 0,

    /// <summary>From <c>LendingNotificationDueEto</c> (<c>Kind = Overdue</c>).</summary>
    LoanOverdue = 1,

    /// <summary>From <c>MemberStandingChangedEto</c> (<c>Kind = StatusChanged</c>, <c>NewStatus = Deactivated</c>).</summary>
    StandingDeactivated = 2,

    /// <summary>From <c>MemberStandingChangedEto</c> (<c>Kind = StatusChanged</c>, <c>NewStatus = Active</c>).</summary>
    StandingReactivated = 3,

    /// <summary>From <c>MemberStandingChangedEto</c> (<c>Kind = RoleChanged</c>).</summary>
    StandingRoleChanged = 4,

    /// <summary>From <c>MemberStandingChangedEto</c> (<c>Kind = RatingOutcome</c>, <c>CrossedLowRatingThreshold = true</c>).</summary>
    StandingLowRatingCrossed = 5
}

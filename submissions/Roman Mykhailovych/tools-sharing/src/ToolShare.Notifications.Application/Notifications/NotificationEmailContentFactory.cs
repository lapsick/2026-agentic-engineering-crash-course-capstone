namespace ToolShare.Notifications.Notifications;

/// <summary>Composes an email subject/body for a generated notification, reusing its <see cref="Notification.DisplayText"/> as the core message.</summary>
public static class NotificationEmailContentFactory
{
    public static (string Subject, string Body) Build(NotificationKind kind, string displayText)
    {
        var subject = kind switch
        {
            NotificationKind.LoanReturnReminder => "Reminder: a loan is due soon",
            NotificationKind.LoanOverdue => "A loan is overdue",
            NotificationKind.StandingDeactivated => "Your membership has been deactivated",
            NotificationKind.StandingReactivated => "Your membership has been reactivated",
            NotificationKind.StandingRoleChanged => "Your community role has changed",
            NotificationKind.StandingLowRatingCrossed => "Your reliability rating has changed",
            _ => "ToolShare notification"
        };

        return (subject, displayText);
    }
}

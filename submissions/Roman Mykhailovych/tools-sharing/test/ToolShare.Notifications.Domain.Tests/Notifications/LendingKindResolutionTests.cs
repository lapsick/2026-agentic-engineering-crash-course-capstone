using Shouldly;
using ToolShare.Lending.Loans;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-001/FR-002: which Lending-sourced kinds warrant a notification.</summary>
public class LendingKindResolutionTests
{
    [Fact]
    public void ReturnReminder_maps_to_LoanReturnReminder()
    {
        NotificationKindResolver.TryMapLendingKind(LendingNotificationKind.ReturnReminder)
            .ShouldBe(NotificationKind.LoanReturnReminder);
    }

    [Fact]
    public void Overdue_maps_to_LoanOverdue()
    {
        NotificationKindResolver.TryMapLendingKind(LendingNotificationKind.Overdue)
            .ShouldBe(NotificationKind.LoanOverdue);
    }
}

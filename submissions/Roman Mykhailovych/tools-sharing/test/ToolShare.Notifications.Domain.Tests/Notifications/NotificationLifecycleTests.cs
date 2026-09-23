using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace ToolShare.Notifications.Notifications;

public class NotificationLifecycleTests
{
    private static readonly DateTime Now = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_with_member_email_yields_delivered_inapp_and_pending_email_records()
    {
        var notification = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationKind.LoanReturnReminder, "Your loan is due soon.", Now,
            originatingLoanId: Guid.NewGuid(), originatingToolInstanceId: Guid.NewGuid(), originatingChangedAt: null,
            memberEmail: "member@example.com");

        notification.DeliveryRecords.Count.ShouldBe(2);

        var inApp = notification.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.InApp);
        inApp.Status.ShouldBe(DeliveryStatus.Delivered);

        var email = notification.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.Email);
        email.Status.ShouldBe(DeliveryStatus.Pending);
    }

    [Fact]
    public void Create_without_member_email_yields_skipped_email_record()
    {
        var notification = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationKind.LoanOverdue, "Your loan is overdue.", Now,
            originatingLoanId: Guid.NewGuid(), originatingToolInstanceId: Guid.NewGuid(), originatingChangedAt: null,
            memberEmail: null);

        var email = notification.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.Email);
        email.Status.ShouldBe(DeliveryStatus.Skipped);

        var inApp = notification.DeliveryRecords.Single(r => r.Channel == DeliveryChannel.InApp);
        inApp.Status.ShouldBe(DeliveryStatus.Delivered);
    }

    [Fact]
    public void MarkRead_is_write_once()
    {
        var notification = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationKind.StandingRoleChanged, "Your role changed.", Now,
            originatingLoanId: null, originatingToolInstanceId: null, originatingChangedAt: Now,
            memberEmail: null);

        notification.ReadAt.ShouldBeNull();

        var firstReadAt = Now.AddMinutes(5);
        notification.MarkRead(firstReadAt);
        notification.ReadAt.ShouldBe(firstReadAt);

        notification.MarkRead(Now.AddMinutes(10));
        notification.ReadAt.ShouldBe(firstReadAt);
    }
}

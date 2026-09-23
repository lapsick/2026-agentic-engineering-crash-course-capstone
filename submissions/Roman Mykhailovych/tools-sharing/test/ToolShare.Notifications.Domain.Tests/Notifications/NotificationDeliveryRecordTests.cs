using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Notifications.Notifications;

public class NotificationDeliveryRecordTests
{
    private static readonly DateTime Now = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Pending_email_record_transitions_to_delivered_exactly_once()
    {
        var record = NotificationDeliveryRecord.CreatePendingEmail(Guid.NewGuid(), Guid.NewGuid(), Now);

        record.MarkDelivered(Now.AddSeconds(1));

        record.Status.ShouldBe(DeliveryStatus.Delivered);
        record.CompletedAt.ShouldBe(Now.AddSeconds(1));

        Should.Throw<BusinessException>(() => record.MarkDelivered(Now.AddSeconds(2)));
    }

    [Fact]
    public void Pending_email_record_transitions_to_failed_exactly_once()
    {
        var record = NotificationDeliveryRecord.CreatePendingEmail(Guid.NewGuid(), Guid.NewGuid(), Now);

        record.MarkFailed(Now.AddSeconds(1), "SMTP unreachable");

        record.Status.ShouldBe(DeliveryStatus.Failed);
        record.FailureDetail.ShouldBe("SMTP unreachable");

        Should.Throw<BusinessException>(() => record.MarkFailed(Now.AddSeconds(2), "retry"));
    }

    [Fact]
    public void InApp_record_is_already_terminal_and_rejects_any_transition()
    {
        var record = NotificationDeliveryRecord.CreateInApp(Guid.NewGuid(), Guid.NewGuid(), Now);

        record.Status.ShouldBe(DeliveryStatus.Delivered);

        Should.Throw<BusinessException>(() => record.MarkDelivered(Now.AddSeconds(1)));
        Should.Throw<BusinessException>(() => record.MarkFailed(Now.AddSeconds(1), "n/a"));
    }

    [Fact]
    public void Skipped_email_record_is_already_terminal_and_rejects_any_transition()
    {
        var record = NotificationDeliveryRecord.CreateSkippedEmail(Guid.NewGuid(), Guid.NewGuid(), Now);

        record.Status.ShouldBe(DeliveryStatus.Skipped);

        Should.Throw<BusinessException>(() => record.MarkDelivered(Now.AddSeconds(1)));
        Should.Throw<BusinessException>(() => record.MarkFailed(Now.AddSeconds(1), "n/a"));
    }
}

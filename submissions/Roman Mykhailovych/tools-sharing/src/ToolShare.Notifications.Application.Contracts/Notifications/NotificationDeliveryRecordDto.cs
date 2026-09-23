using System;

namespace ToolShare.Notifications.Notifications;

public class NotificationDeliveryRecordDto
{
    public DeliveryChannel Channel { get; set; }

    public DeliveryStatus Status { get; set; }

    public DateTime AttemptedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? FailureDetail { get; set; }
}

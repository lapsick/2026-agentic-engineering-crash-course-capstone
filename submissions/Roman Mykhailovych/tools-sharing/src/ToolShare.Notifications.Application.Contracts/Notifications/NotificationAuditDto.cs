using System;
using System.Collections.Generic;

namespace ToolShare.Notifications.Notifications;

public class NotificationAuditDto : NotificationDto
{
    public Guid MemberId { get; set; }

    public List<NotificationDeliveryRecordDto> DeliveryRecords { get; set; } = new();
}

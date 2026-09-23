using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Notifications.Notifications;

public class NotificationDto : EntityDto<Guid>
{
    public NotificationKind Kind { get; set; }

    public string DisplayText { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }
}

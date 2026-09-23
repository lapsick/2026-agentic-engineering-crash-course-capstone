using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Notifications.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-015, SC-006: "was this member notified, and how" — reads a single notification's full delivery history.</summary>
[Authorize(NotificationsPermissions.Audit)]
public class NotificationAuditAppService : ApplicationService, INotificationAuditAppService
{
    private readonly INotificationRepository _notificationRepository;

    public NotificationAuditAppService(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public virtual async Task<NotificationAuditDto> GetAsync(Guid id)
    {
        var notification = await _notificationRepository.FindWithDetailsAsync(id)
            ?? throw new EntityNotFoundException(typeof(Notification), id);

        return new NotificationAuditDto
        {
            Id = notification.Id,
            MemberId = notification.MemberId,
            Kind = notification.Kind,
            DisplayText = notification.DisplayText,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            DeliveryRecords = notification.DeliveryRecords.Select(r => new NotificationDeliveryRecordDto
            {
                Channel = r.Channel,
                Status = r.Status,
                AttemptedAt = r.AttemptedAt,
                CompletedAt = r.CompletedAt,
                FailureDetail = r.FailureDetail
            }).ToList()
        };
    }
}

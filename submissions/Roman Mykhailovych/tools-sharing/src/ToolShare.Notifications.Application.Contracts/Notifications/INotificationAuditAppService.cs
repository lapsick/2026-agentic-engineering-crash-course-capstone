using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// Administrator-only (FR-015; SC-006), gated by <c>Notifications.Audit</c>.
/// Not a self-service surface — takes an explicit id, the one exception in
/// this module to the "never a parameter, always CurrentUser" self-service
/// pattern, mirroring <c>IMemberAppService</c>'s relationship to
/// <c>IMyMembershipAppService</c>.
/// </summary>
public interface INotificationAuditAppService : IApplicationService
{
    Task<NotificationAuditDto> GetAsync(Guid id);
}

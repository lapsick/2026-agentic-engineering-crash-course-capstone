using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// Self-service (FR-012). Resolves the caller's own notifications from
/// <c>CurrentUser.Id</c> — never a parameter, mirroring
/// <c>IMyLendingAppService</c>/<c>IMyMembershipAppService</c>.
/// </summary>
public interface IMyNotificationsAppService : IApplicationService
{
    /// <summary>Own notifications, most recent first.</summary>
    Task<PagedResultDto<NotificationDto>> GetListAsync(GetMyNotificationsInput input);

    /// <summary>FR-013: how many of the caller's own notifications are unread.</summary>
    Task<int> GetUnreadCountAsync();

    /// <summary>
    /// The caller's own member id, resolved from <c>CurrentUser</c> like every other method here (never
    /// a parameter). The Blazor UI uses it to subscribe to the real-time signal, which is keyed by
    /// member id (007-realtime-notifications) — Blazor cannot reach Membership's contracts to resolve it
    /// itself, so Notifications exposes the caller's own id through its own boundary.
    /// </summary>
    Task<Guid> GetMyMemberIdAsync();

    /// <summary>FR-013/FR-014: marks one of the caller's own notifications read (`NOTIF-02`, write-once); refused for a notification that isn't theirs.</summary>
    Task MarkReadAsync(Guid id);

    /// <summary>FR-013: marks every currently-unread notification owned by the caller read (`NOTIF-03`).</summary>
    Task MarkAllReadAsync();
}

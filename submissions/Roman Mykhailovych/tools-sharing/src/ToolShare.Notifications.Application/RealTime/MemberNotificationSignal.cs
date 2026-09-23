using System;
using System.Threading.Tasks;
using Volo.Abp.Uow;

namespace ToolShare.Notifications.RealTime;

/// <summary>
/// Shared producer-side helper for the three call sites that surface a member's notification change in
/// real time (the two generators for a new notification, and <c>MyNotificationsAppService</c>'s
/// mark-read methods for FR-008). Raises the broadcaster signal AFTER the ambient unit of work commits
/// (research R3) so a subscriber's re-query on another connection sees the change; falls back to an
/// immediate signal when there is no ambient unit of work.
/// </summary>
internal static class MemberNotificationSignal
{
    public static async Task AfterCommitAsync(
        IUnitOfWorkManager unitOfWorkManager,
        IMemberNotificationBroadcaster broadcaster,
        Guid memberId)
    {
        var unitOfWork = unitOfWorkManager.Current;
        if (unitOfWork != null)
        {
            unitOfWork.OnCompleted(() => broadcaster.NotifyAsync(memberId));
        }
        else
        {
            await broadcaster.NotifyAsync(memberId);
        }
    }
}

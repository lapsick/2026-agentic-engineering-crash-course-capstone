using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Membership.Members;
using ToolShare.Notifications.RealTime;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Uow;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// Self-service (FR-012). Resolves the caller's own notifications from
/// <c>CurrentUser.Id</c> — never a parameter, mirroring
/// <c>MyLendingAppService.GetOwnMemberIdOrThrowAsync</c> exactly.
/// </summary>
[Authorize]
public class MyNotificationsAppService : ApplicationService, IMyNotificationsAppService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IMemberStandingAppService _memberStandingAppService;
    private readonly IMemberNotificationBroadcaster _broadcaster;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public MyNotificationsAppService(
        INotificationRepository notificationRepository,
        IMemberStandingAppService memberStandingAppService,
        IMemberNotificationBroadcaster broadcaster,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _notificationRepository = notificationRepository;
        _memberStandingAppService = memberStandingAppService;
        _broadcaster = broadcaster;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public virtual async Task<PagedResultDto<NotificationDto>> GetListAsync(GetMyNotificationsInput input)
    {
        var memberId = await GetOwnMemberIdOrThrowAsync();

        var totalCount = await _notificationRepository.GetCountForMemberAsync(memberId);
        var notifications = await _notificationRepository.GetPagedListForMemberAsync(memberId, input.SkipCount, input.MaxResultCount);

        return new PagedResultDto<NotificationDto>(totalCount, notifications.Select(MapToDto).ToList());
    }

    public virtual async Task<int> GetUnreadCountAsync()
    {
        var memberId = await GetOwnMemberIdOrThrowAsync();

        return await _notificationRepository.GetUnreadCountForMemberAsync(memberId);
    }

    /// <summary>The caller's own member id — used by the Blazor UI to subscribe to the member-keyed real-time signal.</summary>
    public virtual async Task<Guid> GetMyMemberIdAsync()
    {
        return await GetOwnMemberIdOrThrowAsync();
    }

    /// <summary>`NOTIF-02`/FR-014: refused (as "not found," never a distinguishable "forbidden") for a notification that isn't the caller's own.</summary>
    public virtual async Task MarkReadAsync(Guid id)
    {
        var memberId = await GetOwnMemberIdOrThrowAsync();

        var notification = await _notificationRepository.GetAsync(id);
        if (notification.MemberId != memberId)
        {
            throw new EntityNotFoundException(typeof(Notification), id);
        }

        var wasUnread = notification.ReadAt is null;
        notification.MarkRead(Clock.Now);
        await _notificationRepository.UpdateAsync(notification);

        // FR-008: propagate the unread-count change to the member's live sessions (their own toolbar
        // bell, and any other open session) after this unit of work commits (research R3). Skip when the
        // notification was already read — nothing changed, so no session needs to re-query.
        if (wasUnread)
        {
            await MemberNotificationSignal.AfterCommitAsync(_unitOfWorkManager, _broadcaster, memberId);
        }
    }

    /// <summary>`NOTIF-03`: applies to every currently-unread notification owned by the caller in one call.</summary>
    public virtual async Task MarkAllReadAsync()
    {
        var memberId = await GetOwnMemberIdOrThrowAsync();

        var unread = await _notificationRepository.GetUnreadForMemberAsync(memberId);
        if (unread.Count == 0)
        {
            // Nothing changed — do not make live sessions re-query for no reason.
            return;
        }

        var now = Clock.Now;
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
            await _notificationRepository.UpdateAsync(notification);
        }

        // FR-008: one signal after commit so the member's bell and other sessions converge (research R3).
        await MemberNotificationSignal.AfterCommitAsync(_unitOfWorkManager, _broadcaster, memberId);
    }

    protected async Task<Guid> GetOwnMemberIdOrThrowAsync()
    {
        var identityUserId = CurrentUser.Id
            ?? throw new AbpAuthorizationException();

        var standing = await _memberStandingAppService.GetByIdentityUserIdAsync(identityUserId);
        return standing.IsEnrolled
            ? standing.MemberId!.Value
            : throw new AbpAuthorizationException();
    }

    private static NotificationDto MapToDto(Notification notification) => new()
    {
        Id = notification.Id,
        Kind = notification.Kind,
        DisplayText = notification.DisplayText,
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt
    };
}

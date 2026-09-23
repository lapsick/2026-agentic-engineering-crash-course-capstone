using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Notifications.Notifications;

public interface INotificationRepository : IRepository<Notification, Guid>
{
    /// <summary>FR-012: the caller's own notifications, most recent first.</summary>
    Task<List<Notification>> GetPagedListForMemberAsync(Guid memberId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default);

    Task<int> GetCountForMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>FR-013: how many of the caller's own notifications are unread.</summary>
    Task<int> GetUnreadCountForMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>FR-013: every currently-unread notification for one member, for `NOTIF-03`'s mark-all-read.</summary>
    Task<List<Notification>> GetUnreadForMemberAsync(Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>`NOTIF-01`'s dedup key, for the two Lending-sourced kinds.</summary>
    Task<bool> ExistsForLoanAsync(Guid memberId, NotificationKind kind, Guid originatingLoanId, CancellationToken cancellationToken = default);

    /// <summary>`NOTIF-01`'s dedup key, for the three Membership-sourced kinds.</summary>
    Task<bool> ExistsForStandingChangeAsync(Guid memberId, NotificationKind kind, DateTime originatingChangedAt, CancellationToken cancellationToken = default);

    /// <summary>Loaded with its <see cref="Notification.DeliveryRecords"/>, for <c>INotificationAuditAppService</c> (FR-015).</summary>
    Task<Notification?> FindWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
}

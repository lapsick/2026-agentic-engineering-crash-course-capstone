using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ToolShare.Notifications.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Notifications.Notifications;

public class EfCoreNotificationRepository : EfCoreRepository<NotificationsDbContext, Notification, Guid>, INotificationRepository
{
    public EfCoreNotificationRepository(IDbContextProvider<NotificationsDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<Notification>> GetPagedListForMemberAsync(Guid memberId, int skipCount, int maxResultCount, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await dbContext.Notifications
            .Include(n => n.DeliveryRecords)
            .Where(n => n.MemberId == memberId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<int> GetCountForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(n => n.MemberId == memberId)
            .CountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<int> GetUnreadCountForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(n => n.MemberId == memberId && n.ReadAt == null)
            .CountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<Notification>> GetUnreadForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(n => n.MemberId == memberId && n.ReadAt == null)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<bool> ExistsForLoanAsync(Guid memberId, NotificationKind kind, Guid originatingLoanId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .AnyAsync(n => n.MemberId == memberId && n.Kind == kind && n.OriginatingLoanId == originatingLoanId, GetCancellationToken(cancellationToken));
    }

    public async Task<bool> ExistsForStandingChangeAsync(Guid memberId, NotificationKind kind, DateTime originatingChangedAt, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .AnyAsync(n => n.MemberId == memberId && n.Kind == kind && n.OriginatingChangedAt == originatingChangedAt, GetCancellationToken(cancellationToken));
    }

    public async Task<Notification?> FindWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await dbContext.Notifications
            .Include(n => n.DeliveryRecords)
            .FirstOrDefaultAsync(n => n.Id == id, GetCancellationToken(cancellationToken));
    }
}

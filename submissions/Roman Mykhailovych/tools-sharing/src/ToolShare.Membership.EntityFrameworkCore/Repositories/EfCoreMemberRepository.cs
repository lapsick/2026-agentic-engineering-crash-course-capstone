using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ToolShare.Membership.EntityFrameworkCore;
using ToolShare.Membership.Members;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Membership.Repositories;

public class EfCoreMemberRepository : EfCoreRepository<MembershipDbContext, Member, Guid>, IMemberRepository
{
    public EfCoreMemberRepository(IDbContextProvider<MembershipDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<Member?> FindByIdentityUserIdAsync(Guid identityUserId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable
            .FirstOrDefaultAsync(m => m.IdentityUserId == identityUserId, GetCancellationToken(cancellationToken));
    }

    public async Task<List<Member>> GetPagedListAsync(
        string? filter,
        MembershipStatus? status,
        CommunityRole? role,
        string sorting,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, status, role);
        query = ApplySorting(query, sorting);

        return await query.Skip(skip).Take(take).ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<int> GetCountAsync(
        string? filter,
        MembershipStatus? status,
        CommunityRole? role,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, status, role);
        return await query.CountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<Member?> GetWithHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await dbContext.Set<Member>()
            .Include(m => m.StandingHistory)
            .FirstOrDefaultAsync(m => m.Id == id, GetCancellationToken(cancellationToken));
    }

    public async Task<int> CountActiveAdministratorsAsync(Guid? excludedMemberId = null, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        var query = queryable.Where(m => m.Role == CommunityRole.Administrator && m.Status == MembershipStatus.Active);
        if (excludedMemberId.HasValue)
        {
            query = query.Where(m => m.Id != excludedMemberId.Value);
        }

        return await query.CountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<bool> HasOutcomeAsync(Guid occurrenceId, ReliabilityOutcomeType outcomeType, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await dbContext.Set<MemberStandingChange>()
            .AnyAsync(
                h => h.OccurrenceId == occurrenceId && h.OutcomeType == outcomeType,
                GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<Member>> BuildFilteredQueryAsync(string? filter, MembershipStatus? status, CommunityRole? role)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var normalizedFilter = filter.Trim().ToLower();
            query = query.Where(m =>
                m.DisplayName.ToLower().Contains(normalizedFilter) ||
                m.Email.ToLower().Contains(normalizedFilter));
        }

        if (status.HasValue)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        if (role.HasValue)
        {
            query = query.Where(m => m.Role == role.Value);
        }

        return query;
    }

    private static IQueryable<Member> ApplySorting(IQueryable<Member> query, string sorting)
    {
        var normalized = (sorting ?? string.Empty).Trim().ToLowerInvariant();

        return normalized switch
        {
            "displayname" => query.OrderBy(m => m.DisplayName),
            "displayname desc" => query.OrderByDescending(m => m.DisplayName),
            "email" => query.OrderBy(m => m.Email),
            "email desc" => query.OrderByDescending(m => m.Email),
            "status" => query.OrderBy(m => m.Status),
            "status desc" => query.OrderByDescending(m => m.Status),
            "role" => query.OrderBy(m => m.Role),
            "role desc" => query.OrderByDescending(m => m.Role),
            "currentrating" => query.OrderBy(m => m.CurrentRating),
            "currentrating desc" => query.OrderByDescending(m => m.CurrentRating),
            "enrolledat" => query.OrderBy(m => m.EnrolledAt),
            "enrolledat desc" => query.OrderByDescending(m => m.EnrolledAt),
            _ => query.OrderBy(m => m.DisplayName)
        };
    }
}

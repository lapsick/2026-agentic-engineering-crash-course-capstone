using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ToolShare.Lending.EntityFrameworkCore;
using ToolShare.Lending.Loans;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Lending.Repositories;

public class EfCoreLoanRepository : EfCoreRepository<LendingDbContext, Loan, Guid>, ILoanRepository
{
    public EfCoreLoanRepository(IDbContextProvider<LendingDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<Loan?> GetOpenForInstanceAsync(Guid toolInstanceId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(l => l.ToolInstanceId == toolInstanceId && l.ReturnedAt == null)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<Loan>> GetOpenForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(l => l.MemberId == memberId && l.ReturnedAt == null)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<Loan>> GetApproachingReminderAsync(DateTime asOf, int leadDays, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        var threshold = DateOnly.FromDateTime(asOf).AddDays(leadDays);

        return await queryable
            .Where(l => l.ReturnedAt == null && l.ReminderSentAt == null && l.PlannedReturnDate <= threshold)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<Loan>> GetNewlyOverdueAsync(DateTime asOf, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        var today = DateOnly.FromDateTime(asOf);

        return await queryable
            .Where(l => l.ReturnedAt == null && !l.IsOverdue && l.PlannedReturnDate < today)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<Loan>> GetPagedListAsync(Guid? memberId, bool onlyOverdue, string sorting, int skipCount, int maxResultCount, CancellationToken cancellationToken = default)
    {
        var queryable = await ApplyFilterAsync(memberId, onlyOverdue);

        return await ApplySorting(queryable, sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<int> GetCountAsync(Guid? memberId, bool onlyOverdue, CancellationToken cancellationToken = default)
    {
        var queryable = await ApplyFilterAsync(memberId, onlyOverdue);
        return await queryable.CountAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<Loan>> ApplyFilterAsync(Guid? memberId, bool onlyOverdue)
    {
        var queryable = await GetQueryableAsync();

        if (memberId.HasValue)
        {
            queryable = queryable.Where(l => l.MemberId == memberId.Value);
        }

        if (onlyOverdue)
        {
            queryable = queryable.Where(l => l.IsOverdue && l.ReturnedAt == null);
        }

        return queryable;
    }

    private static IQueryable<Loan> ApplySorting(IQueryable<Loan> query, string sorting)
    {
        var normalized = (sorting ?? string.Empty).Trim().ToLowerInvariant();

        return normalized switch
        {
            "checkedoutat" => query.OrderBy(l => l.CheckedOutAt),
            "plannedreturndate" => query.OrderBy(l => l.PlannedReturnDate),
            "plannedreturndate desc" => query.OrderByDescending(l => l.PlannedReturnDate),
            _ => query.OrderByDescending(l => l.CheckedOutAt)
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ToolShare.Lending.EntityFrameworkCore;
using ToolShare.Lending.Reservations;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Lending.Repositories;

public class EfCoreWaitlistEntryRepository : EfCoreRepository<LendingDbContext, WaitlistEntry, Guid>, IWaitlistEntryRepository
{
    public EfCoreWaitlistEntryRepository(IDbContextProvider<LendingDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<WaitlistEntry?> GetEarliestWaitingAsync(Guid toolInstanceId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(w => w.ToolInstanceId == toolInstanceId && w.OfferState == WaitlistOfferState.Waiting)
            .OrderBy(w => w.JoinedAt)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<WaitlistEntry?> FindActiveForMemberAndInstanceAsync(Guid memberId, Guid toolInstanceId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(w =>
                w.MemberId == memberId &&
                w.ToolInstanceId == toolInstanceId &&
                (w.OfferState == WaitlistOfferState.Waiting || w.OfferState == WaitlistOfferState.Offered))
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<WaitlistEntry>> GetExpiredOffersAsync(DateTime asOf, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(w => w.OfferState == WaitlistOfferState.Offered && w.OfferExpiresAt < asOf)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<WaitlistEntry?> FindOfferedForInstanceAsync(Guid toolInstanceId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(w => w.ToolInstanceId == toolInstanceId && w.OfferState == WaitlistOfferState.Offered)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }
}

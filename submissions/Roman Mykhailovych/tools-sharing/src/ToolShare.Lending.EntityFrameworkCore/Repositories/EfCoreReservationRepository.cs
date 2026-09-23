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

public class EfCoreReservationRepository : EfCoreRepository<LendingDbContext, Reservation, Guid>, IReservationRepository
{
    public EfCoreReservationRepository(IDbContextProvider<LendingDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<bool> HasOverlapAsync(Guid toolInstanceId, DateOnly startDate, DateOnly endDate, Guid? excludeReservationId = null, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        var query = queryable.Where(r =>
            r.ToolInstanceId == toolInstanceId &&
            r.Status == ReservationStatus.Active &&
            r.StartDate <= endDate &&
            r.EndDate >= startDate);

        if (excludeReservationId.HasValue)
        {
            query = query.Where(r => r.Id != excludeReservationId.Value);
        }

        return await query.AnyAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<int> GetActiveCountForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(r => r.MemberId == memberId && r.Status == ReservationStatus.Active)
            .CountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<Reservation>> GetActiveForInstanceAsync(Guid toolInstanceId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(r => r.ToolInstanceId == toolInstanceId && r.Status == ReservationStatus.Active)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}

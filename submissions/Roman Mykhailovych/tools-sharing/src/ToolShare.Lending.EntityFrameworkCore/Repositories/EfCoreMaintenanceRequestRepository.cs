using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ToolShare.Lending.EntityFrameworkCore;
using ToolShare.Lending.Maintenance;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Lending.Repositories;

public class EfCoreMaintenanceRequestRepository : EfCoreRepository<LendingDbContext, MaintenanceRequest, Guid>, IMaintenanceRequestRepository
{
    public EfCoreMaintenanceRequestRepository(IDbContextProvider<LendingDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<MaintenanceRequest?> GetOpenForInstanceAsync(Guid toolInstanceId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(m => m.ToolInstanceId == toolInstanceId && m.Status == MaintenanceRequestStatus.Open)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<MaintenanceRequest>> GetAllOpenAsync(CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        return await queryable
            .Where(m => m.Status == MaintenanceRequestStatus.Open)
            .OrderBy(m => m.OpenedAt)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}

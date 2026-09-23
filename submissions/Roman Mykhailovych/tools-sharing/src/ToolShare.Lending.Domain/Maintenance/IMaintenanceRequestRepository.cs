using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Lending.Maintenance;

public interface IMaintenanceRequestRepository : IRepository<MaintenanceRequest, Guid>
{
    /// <summary>MAINT-01's pre-check: the open request for the instance, if any.</summary>
    Task<MaintenanceRequest?> GetOpenForInstanceAsync(Guid toolInstanceId, CancellationToken cancellationToken = default);

    /// <summary>The maintenance queue (US3): every currently-open request, oldest first.</summary>
    Task<List<MaintenanceRequest>> GetAllOpenAsync(CancellationToken cancellationToken = default);
}

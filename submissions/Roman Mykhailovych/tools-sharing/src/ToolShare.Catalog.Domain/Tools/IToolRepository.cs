using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Catalog.Tools;

public interface IToolRepository : IRepository<Tool, Guid>
{
    /// <summary>Enforces TR-04's delete guard.</summary>
    Task<bool> AnyInstanceAssignedAsync(Guid toolId, CancellationToken cancellationToken = default);

    /// <summary>The tool plus its instances — separate aggregates, joined here for read convenience only.</summary>
    Task<(Tool Tool, List<ToolInstance> Instances)?> GetWithInstancesAsync(
        Guid id,
        bool includeRetiredInstances = false,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(
        string? normalizedFilter = null,
        Guid? categoryId = null,
        bool onlyAvailable = false,
        CancellationToken cancellationToken = default);

    Task<List<Tool>> GetPagedListAsync(
        string? normalizedFilter,
        Guid? categoryId,
        bool onlyAvailable,
        bool includeRetiredInstances,
        string sorting,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}

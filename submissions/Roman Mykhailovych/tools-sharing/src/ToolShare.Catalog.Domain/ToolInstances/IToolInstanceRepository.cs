using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Catalog.ToolInstances;

public interface IToolInstanceRepository : IRepository<ToolInstance, Guid>
{
    Task<ToolInstance?> FindByNormalizedSerialNumberAsync(string normalizedSerialNumber, CancellationToken cancellationToken = default);

    /// <summary>Enforces IR-02's catalog-wide uniqueness pre-check.</summary>
    Task<bool> AnyBySerialNumberAsync(string normalizedSerialNumber, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<List<ToolInstance>> GetListByToolIdAsync(Guid toolId, bool includeRetired = false, CancellationToken cancellationToken = default);

    /// <summary>Includes photos and full history (HR-03 chronological order).</summary>
    Task<ToolInstance?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Backs <c>IToolInstanceLookupAppService.FindAsync</c> (T115): denormalized
    /// projection joining Tool/Category names, respecting the soft-delete model
    /// filter. Never excludes retired instances — the public contract returns
    /// them (a downstream module must resolve historical references).
    /// </summary>
    Task<ToolInstanceLookupRow?> FindLookupAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Batch form of <see cref="FindLookupAsync"/>. Unknown ids are simply absent from the result.</summary>
    Task<List<ToolInstanceLookupRow>> GetLookupByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<int> GetLookupCountAsync(
        Guid? toolId,
        Guid? categoryId,
        string? normalizedSerialNumberFilter,
        ToolCondition? condition,
        bool onlyAvailable,
        bool includeRetired,
        CancellationToken cancellationToken = default);

    /// <summary>Joins Tool/Category names for the public lookup query (T114). IncludeRetired/OnlyAvailable per contracts/catalog-public-contracts.md.</summary>
    Task<List<ToolInstanceLookupRow>> GetLookupListAsync(
        Guid? toolId,
        Guid? categoryId,
        string? normalizedSerialNumberFilter,
        ToolCondition? condition,
        bool onlyAvailable,
        bool includeRetired,
        string sorting,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}

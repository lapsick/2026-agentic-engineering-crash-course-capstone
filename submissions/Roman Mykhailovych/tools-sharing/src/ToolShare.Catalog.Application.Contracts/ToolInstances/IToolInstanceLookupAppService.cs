using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Read-only lookup of tool instances for other modules (FR-017, FR-019). This
/// is the Tier 1 public boundary — consumers must depend on this interface and
/// its DTOs only, never on Catalog's Domain or EntityFrameworkCore layer (see
/// contracts/catalog-public-contracts.md and CLAUDE.md's "Stability tiers"
/// section). No method here throws for "not found"; absence is expressed as
/// <c>null</c>, an omitted element, or <c>false</c>, and no method mutates state.
/// </summary>
public interface IToolInstanceLookupAppService : IApplicationService
{
    /// <summary>Returns the instance, or null when the id is unknown or soft-deleted. Retired instances are returned.</summary>
    Task<ToolInstanceLookupDto?> FindAsync(Guid id);

    /// <summary>Batch form of <see cref="FindAsync"/>. Unknown ids are omitted. Empty input yields an empty list.</summary>
    Task<List<ToolInstanceLookupDto>> GetByIdsAsync(IEnumerable<Guid> ids);

    /// <summary>Paged, filtered query. Default MaxResultCount 10, hard cap 100.</summary>
    Task<PagedResultDto<ToolInstanceLookupDto>> GetListAsync(ToolInstanceLookupFilterDto input);

    /// <summary>True iff the instance exists and IsAvailable holds. An unknown id returns false rather than throwing.</summary>
    Task<bool> IsAvailableAsync(Guid id);
}

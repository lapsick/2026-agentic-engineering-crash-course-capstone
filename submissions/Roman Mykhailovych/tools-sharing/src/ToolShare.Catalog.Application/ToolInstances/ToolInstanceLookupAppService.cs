using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Tier 1 public boundary implementation (FR-017, FR-019, SC-007). Bare
/// <c>[Authorize]</c> — this is a read-only contract for any authenticated
/// caller, not gated behind a Catalog management permission (mirrors the
/// browse-operation pattern used by <c>ToolAppService.GetListAsync</c>).
/// Consumers resolve <see cref="IToolInstanceLookupAppService"/> from DI and
/// never name this class.
/// </summary>
[Authorize]
public class ToolInstanceLookupAppService : ApplicationService, IToolInstanceLookupAppService
{
    private readonly IToolInstanceRepository _toolInstanceRepository;

    public ToolInstanceLookupAppService(IToolInstanceRepository toolInstanceRepository)
    {
        _toolInstanceRepository = toolInstanceRepository;
    }

    public virtual async Task<ToolInstanceLookupDto?> FindAsync(Guid id)
    {
        var row = await _toolInstanceRepository.FindLookupAsync(id);
        return row is null ? null : MapToDto(row);
    }

    public virtual async Task<List<ToolInstanceLookupDto>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var rows = await _toolInstanceRepository.GetLookupByIdsAsync(ids);
        return rows.Select(MapToDto).ToList();
    }

    public virtual async Task<PagedResultDto<ToolInstanceLookupDto>> GetListAsync(ToolInstanceLookupFilterDto input)
    {
        var normalizedSerialNumberFilter = string.IsNullOrWhiteSpace(input.SerialNumber)
            ? null
            : CatalogTextNormalizer.Normalize(input.SerialNumber);

        var totalCount = await _toolInstanceRepository.GetLookupCountAsync(
            input.ToolId,
            input.CategoryId,
            normalizedSerialNumberFilter,
            input.Condition,
            input.OnlyAvailable ?? false,
            input.IncludeRetired);

        var rows = await _toolInstanceRepository.GetLookupListAsync(
            input.ToolId,
            input.CategoryId,
            normalizedSerialNumberFilter,
            input.Condition,
            input.OnlyAvailable ?? false,
            input.IncludeRetired,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        return new PagedResultDto<ToolInstanceLookupDto>(totalCount, rows.Select(MapToDto).ToList());
    }

    public virtual async Task<bool> IsAvailableAsync(Guid id)
    {
        var row = await _toolInstanceRepository.FindLookupAsync(id);
        return row?.IsAvailable ?? false;
    }

    private static ToolInstanceLookupDto MapToDto(ToolInstanceLookupRow row) => new()
    {
        Id = row.Id,
        ToolId = row.ToolId,
        ToolName = row.ToolName,
        CategoryId = row.CategoryId,
        CategoryName = row.CategoryName,
        SerialNumber = row.SerialNumber,
        Condition = row.Condition,
        CirculationState = row.CirculationState,
        IsAvailable = row.IsAvailable
    };
}

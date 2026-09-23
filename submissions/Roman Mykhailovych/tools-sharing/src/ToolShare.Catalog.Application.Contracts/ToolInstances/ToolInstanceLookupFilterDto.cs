using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Tier 1 filter for <see cref="IToolInstanceLookupAppService.GetListAsync"/>. See
/// contracts/catalog-public-contracts.md for field semantics. <see cref="Sorting"/>
/// accepts SerialNumber/Condition/CirculationState/CreationTime (default
/// "SerialNumber ASC"); an unrecognized value falls back to the default rather
/// than throwing.
/// </summary>
public class ToolInstanceLookupFilterDto : PagedAndSortedResultRequestDto
{
    public Guid? ToolId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? SerialNumber { get; set; }
    public ToolCondition? Condition { get; set; }
    public bool? OnlyAvailable { get; set; }
    public bool IncludeRetired { get; set; }
}

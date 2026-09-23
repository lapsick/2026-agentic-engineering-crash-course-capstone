using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Tier 1 (frozen once shipped, see contracts/README.md). The only tool-instance
/// shape a downstream module (Lending, Maintenance, ...) may depend on — never
/// <c>ToolInstanceDto</c>/<c>ToolInstanceDetailDto</c>, which are Tier 2 and
/// consumed only by this module's own Blazor UI. Changes here are additive only.
/// </summary>
public class ToolInstanceLookupDto : EntityDto<Guid>
{
    public Guid ToolId { get; set; }
    public string ToolName { get; set; } = default!;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = default!;
    public string SerialNumber { get; set; } = default!;
    public ToolCondition Condition { get; set; }
    public ToolInstanceCirculationState CirculationState { get; set; }
    public bool IsAvailable { get; set; }
}

using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.ToolInstances;

public class ToolInstanceDto : AuditedEntityDto<Guid>
{
    public Guid ToolId { get; set; }
    public string SerialNumber { get; set; } = default!;
    public ToolCondition Condition { get; set; }
    public ToolInstanceCirculationState CirculationState { get; set; }
    public bool IsAvailable { get; set; }
    public string? RetirementReason { get; set; }
    public DateTime? RetiredAt { get; set; }
    public string? Notes { get; set; }
    public Guid? PrimaryPhotoId { get; set; }
    public string ConcurrencyStamp { get; set; } = default!;
}

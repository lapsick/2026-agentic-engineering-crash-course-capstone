using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Catalog.ToolInstances;

public class ToolInstanceStateChangeDto : EntityDto<Guid>
{
    public ToolCondition? PreviousCondition { get; set; }
    public ToolCondition NewCondition { get; set; }
    public ToolInstanceCirculationState? PreviousCirculationState { get; set; }
    public ToolInstanceCirculationState NewCirculationState { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
}

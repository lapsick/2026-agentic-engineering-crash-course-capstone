using System;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Raised whenever a tool instance's condition and/or circulation state changes,
/// including its initial registration and its retirement (FR-018). Published via
/// <c>AddLocalEvent</c> on the aggregate, so it is dispatched only if persistence
/// succeeds. Lives in Domain.Shared (not Application.Contracts) because Domain
/// raises it and cannot reference Application.Contracts.
/// </summary>
[Serializable]
public class ToolInstanceStateChangedEto
{
    public Guid ToolInstanceId { get; set; }
    public Guid ToolId { get; set; }
    public string SerialNumber { get; set; } = default!;

    public ToolCondition? PreviousCondition { get; set; }
    public ToolCondition NewCondition { get; set; }

    public ToolInstanceCirculationState? PreviousCirculationState { get; set; }
    public ToolInstanceCirculationState NewCirculationState { get; set; }

    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid? ChangedByUserId { get; set; }
}

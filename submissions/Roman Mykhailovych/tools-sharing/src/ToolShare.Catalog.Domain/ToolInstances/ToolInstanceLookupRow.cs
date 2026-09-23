using System;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Denormalized query projection backing the public lookup service (T114/T115).
/// Deliberately distinct from the Tier 1 <c>ToolInstanceLookupDto</c> in
/// Application.Contracts: Domain must never reference Application.Contracts, so
/// this is the Domain-side shape the repository returns, and the Application
/// layer maps it onto the public DTO.
/// </summary>
public class ToolInstanceLookupRow
{
    public Guid Id { get; set; }
    public Guid ToolId { get; set; }
    public string ToolName { get; set; } = default!;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = default!;
    public string SerialNumber { get; set; } = default!;
    public ToolCondition Condition { get; set; }
    public ToolInstanceCirculationState CirculationState { get; set; }
    public bool IsAvailable { get; set; }
}

namespace ToolShare.Catalog;

/// <summary>
/// The fixed 4-level condition scale from the product spec. Frozen — no value
/// may be added, removed or renumbered by this feature (FR-004).
/// </summary>
public enum ToolCondition
{
    New = 0,
    Good = 1,
    Worn = 2,
    Damaged = 3
}

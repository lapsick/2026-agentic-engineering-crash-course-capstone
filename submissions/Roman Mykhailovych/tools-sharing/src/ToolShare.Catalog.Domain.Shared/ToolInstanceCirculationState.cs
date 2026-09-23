namespace ToolShare.Catalog;

/// <summary>
/// Circulation state of a tool instance. <see cref="OnLoan"/> and
/// <see cref="UnderMaintenance"/> are introduced by feature 004 (Lending),
/// via the inbound <c>IToolInstanceCirculationReportingAppService</c>
/// contract (contracts/catalog-extension.md) — additive only, so every
/// existing consumer of this enum keeps working unchanged. Future values may
/// be appended at higher numeric values — consumers should treat this enum
/// defensively (a <c>default</c> switch arm) rather than assume exhaustiveness.
/// </summary>
public enum ToolInstanceCirculationState
{
    InCirculation = 0,
    Retired = 1,
    OnLoan = 2,
    UnderMaintenance = 3
}

namespace ToolShare.Lending;

/// <summary>
/// How a <see cref="Maintenance.MaintenanceRequest"/> came to be opened
/// (008 data-model.md `MAINT-04`). Every request has exactly one origin.
/// New values may only be appended at higher numeric values; a consumer MUST
/// use a <c>default</c> switch arm.
/// </summary>
public enum MaintenanceRequestOrigin
{
    /// <summary>Opened by a return recording a worse condition than at checkout (004 FR-014); linked to that loan.</summary>
    ReturnTriggered = 0,

    /// <summary>Reported directly by a Librarian for an instance in circulation and not on loan (008 FR-001).</summary>
    OutOfBand = 1
}

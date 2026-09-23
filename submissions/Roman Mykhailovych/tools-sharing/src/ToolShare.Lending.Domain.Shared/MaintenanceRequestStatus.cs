namespace ToolShare.Lending;

/// <summary>
/// The lifecycle of a <see cref="Maintenance.MaintenanceRequest"/>
/// (data-model.md `MAINT-01`/`MAINT-02`). Internal to Lending's own operation —
/// see <see cref="ReservationStatus"/>'s remarks on why this carries no
/// cross-module numeric-stability obligation.
/// </summary>
public enum MaintenanceRequestStatus
{
    /// <summary>Instance unavailable until closed.</summary>
    Open = 0,

    /// <summary>Cost recorded, instance available again. Terminal.</summary>
    Closed = 1
}

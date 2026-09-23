namespace ToolShare.Lending;

/// <summary>
/// The lifecycle of a <see cref="Reservations.Reservation"/>. Internal to
/// Lending's own operation — not part of any published boundary (contracts/README.md),
/// so unlike Catalog's/Membership's frozen public enums this carries no
/// cross-module numeric-stability obligation; only this module's own EF Core
/// mapping depends on the values staying stable across migrations.
/// </summary>
public enum ReservationStatus
{
    /// <summary>Held, not yet checked out; may still be cancelled.</summary>
    Active = 0,

    /// <summary>Ended before checkout — by the member (RES-06) or by the system (RES-08). Terminal.</summary>
    Cancelled = 1,

    /// <summary>Realized as a <see cref="Loans.Loan"/> (RES-07). Terminal.</summary>
    CheckedOut = 2
}

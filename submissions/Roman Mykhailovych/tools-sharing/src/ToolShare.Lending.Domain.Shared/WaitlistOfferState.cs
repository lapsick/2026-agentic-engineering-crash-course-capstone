namespace ToolShare.Lending;

/// <summary>
/// The lifecycle of a <see cref="Reservations.WaitlistEntry"/>'s time-boxed
/// offer (data-model.md `WL-03`–`WL-05`). Internal to Lending's own operation —
/// see <see cref="ReservationStatus"/>'s remarks on why this carries no
/// cross-module numeric-stability obligation.
/// </summary>
public enum WaitlistOfferState
{
    /// <summary>In line, no offer yet.</summary>
    Waiting = 0,

    /// <summary>Currently holds the time-boxed offer.</summary>
    Offered = 1,

    /// <summary>Offer accepted; realized as a <see cref="Reservations.Reservation"/>. Terminal.</summary>
    Confirmed = 2,

    /// <summary>Offer lapsed unconfirmed; the next entry (if any) is offered. Terminal.</summary>
    Expired = 3
}

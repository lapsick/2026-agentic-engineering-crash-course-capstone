namespace ToolShare.Notifications;

/// <summary>
/// Business-exception codes raised by the Notifications domain, localized
/// through <see cref="Localization.NotificationsResource"/>. Mirrors the
/// pattern documented for Catalog, Membership, and Lending.
/// </summary>
public static class NotificationsDomainErrorCodes
{
    /// <summary>
    /// Defensive guard for entity-internal state transitions that a caller
    /// cannot normally trigger through the application layer (e.g. transitioning
    /// an already-terminal <c>NotificationDeliveryRecord</c> a second time,
    /// `DR-01`) — reachable only if an invariant this module itself guarantees
    /// elsewhere has been violated.
    /// </summary>
    public const string InvalidStateTransition = "Notifications:InvalidStateTransition";
}

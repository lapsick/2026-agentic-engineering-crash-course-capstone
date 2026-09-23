namespace ToolShare.Notifications;

public enum DeliveryStatus
{
    /// <summary>Reached the member — in-app: the row exists; email: the send call succeeded.</summary>
    Delivered = 0,

    /// <summary>Enqueued, not yet attempted (email only — in-app never uses this).</summary>
    Pending = 1,

    /// <summary>Attempted and did not succeed (e.g. mail transport unreachable).</summary>
    Failed = 2,

    /// <summary>Not attempted by design — the member had no email address on file (FR-009).</summary>
    Skipped = 3
}

using System;
using System.Threading.Tasks;

namespace ToolShare.Notifications.RealTime;

/// <summary>
/// In-process, single-host push seam (007-realtime-notifications, research R2). A live Blazor circuit
/// subscribes for its OWN member id; producer-side call sites — the notification generators, and
/// <c>MyNotificationsAppService</c>'s mark-read methods — raise a contentless signal after their unit of
/// work commits (research R3). Delivery is best-effort and exception-isolated: a subscriber failure is
/// caught, never propagating to the producer, and — because the signal is raised only after commit — it
/// cannot block, delay, or fail notification generation, persistence, or email (FR-009). Registered as a
/// singleton.
/// </summary>
public interface IMemberNotificationBroadcaster
{
    /// <summary>
    /// Register interest in a single member's notification changes. Dispose the returned handle on
    /// circuit/component teardown to unsubscribe. <paramref name="onChanged"/> is invoked (with no
    /// payload) whenever <see cref="NotifyAsync"/> runs for this <paramref name="memberId"/>; the
    /// subscriber then re-queries the authoritative store itself (FR-002/FR-003).
    /// </summary>
    IDisposable Subscribe(Guid memberId, Func<Task> onChanged);

    /// <summary>
    /// Publish a contentless "your notifications changed" signal to every current subscriber for
    /// <paramref name="memberId"/>. Subscriber exceptions are caught and logged, never propagated
    /// (FR-009); a member with no subscribers is a no-op.
    /// </summary>
    Task NotifyAsync(Guid memberId);
}

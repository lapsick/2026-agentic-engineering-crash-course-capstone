using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ToolShare.Notifications.RealTime;

/// <summary>
/// Single-host, in-memory implementation of <see cref="IMemberNotificationBroadcaster"/> (research
/// R2/R6). A thread-safe registry keyed by member id holds one callback per live subscription; publish
/// snapshots the current subscribers and isolates each callback's failure so one slow or throwing
/// subscriber can neither block another's delivery nor surface to the producer (FR-009). State is
/// transient — empty at startup, rebuilt as circuits connect, gone at process exit (data-model.md).
/// </summary>
public class InProcessMemberNotificationBroadcaster : IMemberNotificationBroadcaster, ISingletonDependency
{
    // Empty per-member buckets are intentionally NOT removed on unsubscribe: keeping them avoids an
    // add-vs-remove race between Subscribe's GetOrAdd and a concurrent Unsubscribe, at a bounded cost
    // (one empty map per member that ever connected — hundreds at this feature's scale).
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Func<Task>>> _subscribers = new();
    private readonly ILogger<InProcessMemberNotificationBroadcaster> _logger;

    public InProcessMemberNotificationBroadcaster(ILogger<InProcessMemberNotificationBroadcaster> logger)
    {
        _logger = logger;
    }

    public IDisposable Subscribe(Guid memberId, Func<Task> onChanged)
    {
        Check.NotNull(onChanged, nameof(onChanged));

        var subscriptionId = Guid.NewGuid();
        var callbacks = _subscribers.GetOrAdd(memberId, _ => new ConcurrentDictionary<Guid, Func<Task>>());
        callbacks[subscriptionId] = onChanged;

        return new Subscription(this, memberId, subscriptionId);
    }

    public Task NotifyAsync(Guid memberId)
    {
        if (!_subscribers.TryGetValue(memberId, out var callbacks) || callbacks.IsEmpty)
        {
            return Task.CompletedTask;
        }

        // Dispatch to all of the member's sessions concurrently (not one-after-another), so a slow
        // session cannot serialize the others behind it. The snapshot means a Subscribe/Dispose during
        // dispatch cannot corrupt iteration. Each callback's failure is isolated (FR-009).
        var dispatch = callbacks.Values.Select(callback => InvokeIsolatedAsync(callback, memberId)).ToArray();
        return dispatch.Length == 0 ? Task.CompletedTask : Task.WhenAll(dispatch);
    }

    private async Task InvokeIsolatedAsync(Func<Task> callback, Guid memberId)
    {
        try
        {
            await callback.Invoke();
        }
        catch (Exception ex)
        {
            // Best-effort, exception-isolated (FR-009): a subscriber failure never blocks another's
            // delivery, and never propagates to the post-commit producer that raised the signal.
            _logger.LogWarning(
                ex,
                "A real-time notification subscriber for member {MemberId} threw; continuing with the others.",
                memberId);
        }
    }

    private void Unsubscribe(Guid memberId, Guid subscriptionId)
    {
        if (_subscribers.TryGetValue(memberId, out var callbacks))
        {
            callbacks.TryRemove(subscriptionId, out _);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly InProcessMemberNotificationBroadcaster _owner;
        private readonly Guid _memberId;
        private readonly Guid _subscriptionId;
        private bool _disposed;

        public Subscription(InProcessMemberNotificationBroadcaster owner, Guid memberId, Guid subscriptionId)
        {
            _owner = owner;
            _memberId = memberId;
            _subscriptionId = subscriptionId;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.Unsubscribe(_memberId, _subscriptionId);
        }
    }
}

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace ToolShare.Notifications.RealTime;

/// <summary>
/// FR-002/FR-006/FR-009: pure, no-database unit tests for the in-process broadcaster's dispatch,
/// isolation, and unsubscribe semantics. Deliberately does NOT inherit the Testcontainers-backed
/// application test base, so it runs without Docker (research R6).
/// </summary>
public class InProcessMemberNotificationBroadcasterTests
{
    private static InProcessMemberNotificationBroadcaster Create() =>
        new(NullLogger<InProcessMemberNotificationBroadcaster>.Instance);

    [Fact]
    public async Task Signals_all_subscribers_for_the_target_member_and_none_for_another_member()
    {
        var broadcaster = Create();
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();

        int a1 = 0, a2 = 0, b = 0;
        broadcaster.Subscribe(memberA, () => { a1++; return Task.CompletedTask; });
        broadcaster.Subscribe(memberA, () => { a2++; return Task.CompletedTask; });
        broadcaster.Subscribe(memberB, () => { b++; return Task.CompletedTask; });

        await broadcaster.NotifyAsync(memberA);

        a1.ShouldBe(1); // FR-006: every one of the member's subscriptions is signalled
        a2.ShouldBe(1);
        b.ShouldBe(0);  // FR-002: another member is never signalled
    }

    [Fact]
    public async Task Disposing_a_subscription_stops_further_delivery()
    {
        var broadcaster = Create();
        var member = Guid.NewGuid();
        var count = 0;

        var subscription = broadcaster.Subscribe(member, () => { count++; return Task.CompletedTask; });

        await broadcaster.NotifyAsync(member);
        subscription.Dispose();
        await broadcaster.NotifyAsync(member);

        count.ShouldBe(1); // no leak: the disposed subscription is not invoked again
    }

    [Fact]
    public async Task A_throwing_subscriber_is_isolated_and_does_not_block_others_or_surface()
    {
        var broadcaster = Create();
        var member = Guid.NewGuid();
        var healthy = 0;

        broadcaster.Subscribe(member, () => throw new InvalidOperationException("boom"));
        broadcaster.Subscribe(member, () => { healthy++; return Task.CompletedTask; });

        await Should.NotThrowAsync(() => broadcaster.NotifyAsync(member)); // FR-009: never surfaces
        healthy.ShouldBe(1); // the healthy subscriber still ran
    }

    [Fact]
    public async Task Notifying_a_member_with_no_subscribers_is_a_no_op()
    {
        var broadcaster = Create();

        await Should.NotThrowAsync(() => broadcaster.NotifyAsync(Guid.NewGuid()));
    }
}

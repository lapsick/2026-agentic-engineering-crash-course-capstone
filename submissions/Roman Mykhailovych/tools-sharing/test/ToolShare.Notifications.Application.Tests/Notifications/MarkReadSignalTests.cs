using System;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using ToolShare.Notifications.RealTime;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// FR-008: marking read raises exactly one post-commit real-time signal for the caller's member (and no
/// other member's), so the toolbar bell and the member's other sessions converge on the new unread
/// count. Integration test on real PostgreSQL. Uses the no-email member so notification setup does not
/// depend on the (unrelated) email background-job path.
/// </summary>
public class MarkReadSignalTests : NotificationsAuthorizationTestBase
{
    private readonly IMemberNotificationBroadcaster _broadcaster;
    private readonly INotificationRepository _notificationRepository;
    private readonly IMemberRepository _memberRepository;

    public MarkReadSignalTests()
    {
        _broadcaster = GetRequiredService<IMemberNotificationBroadcaster>();
        _notificationRepository = GetRequiredService<INotificationRepository>();
        _memberRepository = GetRequiredService<IMemberRepository>();
    }

    [Fact]
    public async Task MarkReadAsync_signals_the_caller_member_exactly_once_and_no_other_member()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoEmailUserId);
        await CreateLoanNotificationAsync(member!.Id, instance.Id);
        var notificationId = (await _notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10))[0].Id;

        // Subscribe AFTER the notification exists, so only the mark-read signal is counted.
        var memberSignals = 0;
        var unrelatedSignals = 0;
        using var _ = _broadcaster.Subscribe(member.Id, () => { Interlocked.Increment(ref memberSignals); return Task.CompletedTask; });
        using var __ = _broadcaster.Subscribe(Guid.NewGuid(), () => { Interlocked.Increment(ref unrelatedSignals); return Task.CompletedTask; });

        using (Impersonate(BuildPrincipal(NotificationsTestPrincipals.NoEmailUserId, "no-email-user")))
        {
            await GetRequiredService<IMyNotificationsAppService>().MarkReadAsync(notificationId);
        }

        memberSignals.ShouldBe(1);   // FR-008
        unrelatedSignals.ShouldBe(0); // FR-002
    }

    [Fact]
    public async Task MarkAllReadAsync_signals_the_caller_member_once()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoEmailUserId);
        await CreateLoanNotificationAsync(member!.Id, instance.Id);
        await CreateLoanNotificationAsync(member.Id, instance.Id, overdue: true);

        var memberSignals = 0;
        using var _ = _broadcaster.Subscribe(member.Id, () => { Interlocked.Increment(ref memberSignals); return Task.CompletedTask; });

        using (Impersonate(BuildPrincipal(NotificationsTestPrincipals.NoEmailUserId, "no-email-user")))
        {
            await GetRequiredService<IMyNotificationsAppService>().MarkAllReadAsync();
        }

        memberSignals.ShouldBe(1);
    }

    private Task CreateLoanNotificationAsync(Guid memberId, Guid toolInstanceId, bool overdue = false)
    {
        return LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = memberId,
            ToolInstanceId = toolInstanceId,
            Kind = overdue ? LendingNotificationKind.Overdue : LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(overdue ? -1 : 2)),
            RaisedAt = DateTime.UtcNow
        });
    }
}

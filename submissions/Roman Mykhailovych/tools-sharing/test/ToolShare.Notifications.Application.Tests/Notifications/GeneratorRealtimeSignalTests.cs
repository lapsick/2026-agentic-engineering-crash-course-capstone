using System;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership;
using ToolShare.Membership.Members;
using ToolShare.Notifications.RealTime;
using Volo.Abp.Uow;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>
/// FR-001/FR-002/FR-006 + research R3: creating a notification (via either generator) raises exactly one
/// real-time signal, for the target member only, AFTER the row is committed and visible to a re-query on
/// another connection. Integration test on real PostgreSQL.
/// <para>
/// Uses the no-email member deliberately: the real-time signal is independent of the email channel, and
/// the no-email path skips the email background-job enqueue — keeping these tests focused on the signal
/// and free of any unrelated background-jobs concern.
/// </para>
/// </summary>
public class GeneratorRealtimeSignalTests : NotificationsAuthorizationTestBase
{
    private readonly IMemberNotificationBroadcaster _broadcaster;
    private readonly INotificationRepository _notificationRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public GeneratorRealtimeSignalTests()
    {
        _broadcaster = GetRequiredService<IMemberNotificationBroadcaster>();
        _notificationRepository = GetRequiredService<INotificationRepository>();
        _memberRepository = GetRequiredService<IMemberRepository>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
    }

    [Fact]
    public async Task A_loan_notification_signals_the_target_member_exactly_once()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoEmailUserId);

        var signals = 0;
        using var _ = _broadcaster.Subscribe(member!.Id, () => { Interlocked.Increment(ref signals); return Task.CompletedTask; });

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            RaisedAt = DateTime.UtcNow
        });

        signals.ShouldBe(1);
    }

    [Fact]
    public async Task A_standing_change_notification_signals_the_target_member()
    {
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoEmailUserId);

        var signals = 0;
        using var _ = _broadcaster.Subscribe(member!.Id, () => { Interlocked.Increment(ref signals); return Task.CompletedTask; });

        await LocalEventBus.PublishAsync(new MemberStandingChangedEto
        {
            MemberId = member.Id,
            IdentityUserId = member.IdentityUserId,
            Kind = MemberStandingChangeKind.StatusChanged,
            PreviousStatus = MembershipStatus.Active,
            NewStatus = MembershipStatus.Deactivated,
            NewRole = CommunityRole.Member,
            NewRating = 100,
            CrossedLowRatingThreshold = false,
            ChangedAt = DateTime.UtcNow
        });

        signals.ShouldBe(1);
    }

    [Fact]
    public async Task A_different_member_is_never_signalled()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoEmailUserId);
        var unrelatedMemberId = Guid.NewGuid();

        var targetSignals = 0;
        var unrelatedSignals = 0;
        using var _ = _broadcaster.Subscribe(member!.Id, () => { Interlocked.Increment(ref targetSignals); return Task.CompletedTask; });
        using var __ = _broadcaster.Subscribe(unrelatedMemberId, () => { Interlocked.Increment(ref unrelatedSignals); return Task.CompletedTask; });

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.Overdue,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            RaisedAt = DateTime.UtcNow
        });

        targetSignals.ShouldBe(1);   // FR-001
        unrelatedSignals.ShouldBe(0); // FR-002
    }

    [Fact]
    public async Task The_signal_fires_only_after_the_notification_is_committed_and_visible()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoEmailUserId);

        // research R3: re-query on a fresh, independent unit of work from inside the signal callback.
        // Seeing the row proves the signal fired after the generating unit of work committed.
        var observedUnreadCount = -1;
        using var _ = _broadcaster.Subscribe(member!.Id, async () =>
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true);
            observedUnreadCount = await _notificationRepository.GetUnreadCountForMemberAsync(member.Id);
            await uow.CompleteAsync();
        });

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            RaisedAt = DateTime.UtcNow
        });

        observedUnreadCount.ShouldBe(1);
    }
}

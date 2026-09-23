using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-003, FR-004: raising a <see cref="MemberStandingChangedEto"/> for a notification-worthy kind produces exactly one notification; a non-crossing rating outcome produces none.</summary>
public class StandingChangeNotificationGeneratorTests : NotificationsAuthorizationTestBase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IMemberRepository _memberRepository;

    public StandingChangeNotificationGeneratorTests()
    {
        _notificationRepository = GetRequiredService<INotificationRepository>();
        _memberRepository = GetRequiredService<IMemberRepository>();
    }

    [Fact]
    public async Task Deactivation_creates_a_notification()
    {
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.OtherMemberId);

        await LocalEventBus.PublishAsync(new MemberStandingChangedEto
        {
            MemberId = member!.Id,
            IdentityUserId = member.IdentityUserId,
            Kind = MemberStandingChangeKind.StatusChanged,
            PreviousStatus = MembershipStatus.Active,
            NewStatus = MembershipStatus.Deactivated,
            NewRole = CommunityRole.Member,
            NewRating = 100,
            CrossedLowRatingThreshold = false,
            ChangedAt = DateTime.UtcNow
        });

        var notifications = await _notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10);
        notifications.ShouldHaveSingleItem();
        notifications[0].Kind.ShouldBe(NotificationKind.StandingDeactivated);
    }

    [Fact]
    public async Task Role_change_creates_a_notification()
    {
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.OtherMemberId);

        await LocalEventBus.PublishAsync(new MemberStandingChangedEto
        {
            MemberId = member!.Id,
            IdentityUserId = member.IdentityUserId,
            Kind = MemberStandingChangeKind.RoleChanged,
            NewStatus = MembershipStatus.Active,
            PreviousRole = CommunityRole.Member,
            NewRole = CommunityRole.Librarian,
            NewRating = 100,
            CrossedLowRatingThreshold = false,
            ChangedAt = DateTime.UtcNow
        });

        var notifications = await _notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10);
        notifications.ShouldHaveSingleItem();
        notifications[0].Kind.ShouldBe(NotificationKind.StandingRoleChanged);
        notifications[0].DisplayText.ShouldContain("Librarian");
    }

    [Fact]
    public async Task Rating_outcome_crossing_the_threshold_creates_a_notification()
    {
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.OtherMemberId);

        await LocalEventBus.PublishAsync(new MemberStandingChangedEto
        {
            MemberId = member!.Id,
            IdentityUserId = member.IdentityUserId,
            Kind = MemberStandingChangeKind.RatingOutcome,
            NewStatus = MembershipStatus.Active,
            NewRole = CommunityRole.Member,
            PreviousRating = 55,
            NewRating = 45,
            CrossedLowRatingThreshold = true,
            OutcomeType = ReliabilityOutcomeType.OverdueReturn,
            OccurrenceId = Guid.NewGuid(),
            ChangedAt = DateTime.UtcNow
        });

        var notifications = await _notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10);
        notifications.ShouldHaveSingleItem();
        notifications[0].Kind.ShouldBe(NotificationKind.StandingLowRatingCrossed);
    }

    [Fact]
    public async Task Rating_outcome_not_crossing_the_threshold_creates_no_notification()
    {
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.OtherMemberId);

        await LocalEventBus.PublishAsync(new MemberStandingChangedEto
        {
            MemberId = member!.Id,
            IdentityUserId = member.IdentityUserId,
            Kind = MemberStandingChangeKind.RatingOutcome,
            NewStatus = MembershipStatus.Active,
            NewRole = CommunityRole.Member,
            PreviousRating = 100,
            NewRating = 98,
            CrossedLowRatingThreshold = false,
            OutcomeType = ReliabilityOutcomeType.OverdueReturn,
            OccurrenceId = Guid.NewGuid(),
            ChangedAt = DateTime.UtcNow
        });

        var count = await _notificationRepository.GetCountForMemberAsync(member.Id);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task Enrolment_creates_no_notification()
    {
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.OtherMemberId);

        await LocalEventBus.PublishAsync(new MemberStandingChangedEto
        {
            MemberId = member!.Id,
            IdentityUserId = member.IdentityUserId,
            Kind = MemberStandingChangeKind.Enrolled,
            NewStatus = MembershipStatus.Active,
            NewRole = CommunityRole.Member,
            NewRating = 100,
            CrossedLowRatingThreshold = false,
            ChangedAt = DateTime.UtcNow
        });

        var count = await _notificationRepository.GetCountForMemberAsync(member.Id);
        count.ShouldBe(0);
    }
}

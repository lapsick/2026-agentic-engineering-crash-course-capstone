using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-014: a standing-change notification is generated only for the affected member, never any other.</summary>
public class StandingNotificationScopeTests : NotificationsAuthorizationTestBase
{
    [Fact]
    public async Task Only_the_affected_member_receives_the_notification()
    {
        var memberRepository = GetRequiredService<IMemberRepository>();
        var affected = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.OtherMemberId);
        var bystander = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);

        await LocalEventBus.PublishAsync(new MemberStandingChangedEto
        {
            MemberId = affected!.Id,
            IdentityUserId = affected.IdentityUserId,
            Kind = MemberStandingChangeKind.StatusChanged,
            PreviousStatus = MembershipStatus.Active,
            NewStatus = MembershipStatus.Deactivated,
            NewRole = CommunityRole.Member,
            NewRating = 100,
            CrossedLowRatingThreshold = false,
            ChangedAt = DateTime.UtcNow
        });

        var notificationRepository = GetRequiredService<INotificationRepository>();

        var affectedCount = await notificationRepository.GetCountForMemberAsync(affected.Id);
        affectedCount.ShouldBe(1);

        var bystanderCount = await notificationRepository.GetCountForMemberAsync(bystander!.Id);
        bystanderCount.ShouldBe(0);
    }
}

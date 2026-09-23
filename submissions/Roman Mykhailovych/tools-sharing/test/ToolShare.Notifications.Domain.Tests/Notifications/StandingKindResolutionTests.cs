using Shouldly;
using ToolShare.Membership;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-003/FR-004: which Membership-sourced kinds warrant a notification.</summary>
public class StandingKindResolutionTests
{
    [Fact]
    public void StatusChanged_to_Deactivated_maps_to_StandingDeactivated()
    {
        NotificationKindResolver.TryMapStandingChange(MemberStandingChangeKind.StatusChanged, MembershipStatus.Active, MembershipStatus.Deactivated, crossedLowRatingThreshold: false)
            .ShouldBe(NotificationKind.StandingDeactivated);
    }

    [Fact]
    public void StatusChanged_to_Active_maps_to_StandingReactivated()
    {
        NotificationKindResolver.TryMapStandingChange(MemberStandingChangeKind.StatusChanged, MembershipStatus.Deactivated, MembershipStatus.Active, crossedLowRatingThreshold: false)
            .ShouldBe(NotificationKind.StandingReactivated);
    }

    [Fact]
    public void RoleChanged_maps_to_StandingRoleChanged()
    {
        NotificationKindResolver.TryMapStandingChange(MemberStandingChangeKind.RoleChanged, MembershipStatus.Active, MembershipStatus.Active, crossedLowRatingThreshold: false)
            .ShouldBe(NotificationKind.StandingRoleChanged);
    }

    [Fact]
    public void RatingOutcome_crossing_the_threshold_maps_to_StandingLowRatingCrossed()
    {
        NotificationKindResolver.TryMapStandingChange(MemberStandingChangeKind.RatingOutcome, MembershipStatus.Active, MembershipStatus.Active, crossedLowRatingThreshold: true)
            .ShouldBe(NotificationKind.StandingLowRatingCrossed);
    }

    [Fact]
    public void RatingOutcome_not_crossing_the_threshold_produces_no_notification()
    {
        NotificationKindResolver.TryMapStandingChange(MemberStandingChangeKind.RatingOutcome, MembershipStatus.Active, MembershipStatus.Active, crossedLowRatingThreshold: false)
            .ShouldBeNull();
    }

    [Fact]
    public void Enrolled_produces_no_notification()
    {
        NotificationKindResolver.TryMapStandingChange(MemberStandingChangeKind.Enrolled, MembershipStatus.Active, MembershipStatus.Active, crossedLowRatingThreshold: false)
            .ShouldBeNull();
    }
}

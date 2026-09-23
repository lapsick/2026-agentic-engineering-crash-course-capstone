using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Membership.Members;

public class MemberLifecycleTests
{
    private static Member CreateMember()
    {
        return new Member(Guid.NewGuid(), Guid.NewGuid(), "Alice Example", "alice@example.com", DateTime.UtcNow, null);
    }

    [Fact]
    public void Enrolment_sets_the_documented_defaults()
    {
        var identityUserId = Guid.NewGuid();
        var enrolledAt = DateTime.UtcNow;

        var member = new Member(Guid.NewGuid(), identityUserId, "Alice Example", "Alice@Example.com", enrolledAt, null);

        member.IdentityUserId.ShouldBe(identityUserId);
        member.DisplayName.ShouldBe("Alice Example");
        member.Status.ShouldBe(MembershipStatus.Active);
        member.Role.ShouldBe(CommunityRole.Member);
        member.CurrentRating.ShouldBe(100);
        member.EnrolledAt.ShouldBe(enrolledAt);
        member.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Enrolment_rejects_an_empty_display_name()
    {
        var exception = Should.Throw<BusinessException>(() =>
            new Member(Guid.NewGuid(), Guid.NewGuid(), "   ", "alice@example.com", DateTime.UtcNow, null));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.DisplayNameRequired);
    }

    [Fact]
    public void Deactivating_an_already_deactivated_member_is_rejected()
    {
        var member = CreateMember();
        member.Deactivate("no longer participating", DateTime.UtcNow, null);

        var exception = Should.Throw<BusinessException>(() =>
            member.Deactivate("again", DateTime.UtcNow, null));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.AlreadyDeactivated);
    }

    [Fact]
    public void Reactivating_an_active_member_is_rejected()
    {
        var member = CreateMember();

        var exception = Should.Throw<BusinessException>(() =>
            member.Reactivate(DateTime.UtcNow, null));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.AlreadyActive);
    }

    [Fact]
    public void Reactivation_preserves_the_rating_the_member_had_when_deactivated()
    {
        var member = CreateMember();
        member.ApplyOutcome(ReliabilityOutcomeType.ManualAdjustment, -35, occurrenceId: null, reason: "penalty", DateTime.UtcNow, null);
        member.CurrentRating.ShouldBe(65);

        member.Deactivate("leaving", DateTime.UtcNow, null);
        member.Reactivate(DateTime.UtcNow, null);

        member.Status.ShouldBe(MembershipStatus.Active);
        member.CurrentRating.ShouldBe(65);
    }

    [Fact]
    public void Changing_role_to_the_same_role_is_rejected()
    {
        var member = CreateMember();

        var exception = Should.Throw<BusinessException>(() =>
            member.ChangeRole(CommunityRole.Member, DateTime.UtcNow, null));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.RoleUnchanged);
    }

    [Fact]
    public void Changing_role_to_a_different_role_succeeds_and_exactly_one_role_is_held()
    {
        var member = CreateMember();

        member.ChangeRole(CommunityRole.Librarian, DateTime.UtcNow, null);

        member.Role.ShouldBe(CommunityRole.Librarian);
    }

    [Fact]
    public void Outcomes_may_still_be_applied_to_a_deactivated_member()
    {
        var member = CreateMember();
        member.Deactivate("left", DateTime.UtcNow, null);

        member.ApplyOutcome(ReliabilityOutcomeType.OverdueReturn, -10, occurrenceId: Guid.NewGuid(), reason: null, DateTime.UtcNow, null);

        member.CurrentRating.ShouldBe(90);
        member.Status.ShouldBe(MembershipStatus.Deactivated);
    }
}

using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

public class StandingHistoryTests
{
    private static Member CreateMember()
    {
        return new Member(Guid.NewGuid(), Guid.NewGuid(), "Alice Example", "alice@example.com", DateTime.UtcNow, null);
    }

    [Fact]
    public void First_entry_is_always_Enrolled_with_null_previous_values()
    {
        var member = CreateMember();

        member.StandingHistory.Count.ShouldBe(1);

        var entry = member.StandingHistory.Single();
        entry.Kind.ShouldBe(MemberStandingChangeKind.Enrolled);
        entry.PreviousStatus.ShouldBeNull();
        entry.PreviousRole.ShouldBeNull();
        entry.NewStatus.ShouldBe(MembershipStatus.Active);
        entry.NewRole.ShouldBe(CommunityRole.Member);
    }

    [Fact]
    public void One_entry_is_appended_per_transition()
    {
        var member = CreateMember();

        member.ChangeRole(CommunityRole.Librarian, DateTime.UtcNow, null);
        member.Deactivate("left", DateTime.UtcNow, null);
        member.Reactivate(DateTime.UtcNow, null);
        member.ApplyOutcome(ReliabilityOutcomeType.OverdueReturn, -10, occurrenceId: Guid.NewGuid(), reason: null, DateTime.UtcNow, null);

        // enrolled + role changed + deactivated + reactivated + rating outcome
        member.StandingHistory.Count.ShouldBe(5);
    }

    [Fact]
    public void Exactly_one_local_event_is_raised_per_transition()
    {
        var member = CreateMember();
        member.ClearLocalEvents();

        member.ChangeRole(CommunityRole.Librarian, DateTime.UtcNow, null);
        member.GetLocalEvents().Count().ShouldBe(1);

        member.ClearLocalEvents();
        member.Deactivate("left", DateTime.UtcNow, null);
        member.GetLocalEvents().Count().ShouldBe(1);
    }

    [Fact]
    public void History_has_no_public_mutation_surface()
    {
        var historyType = typeof(MemberStandingChange);

        historyType.GetMethods()
            .Where(m => m.DeclaringType == historyType)
            .Any(m => m.Name is "Update" or "Delete" or "Remove")
            .ShouldBeFalse();

        foreach (var property in historyType.GetProperties())
        {
            property.SetMethod?.IsPublic.ShouldNotBe(true, $"{property.Name} must not have a public setter");
        }
    }
}

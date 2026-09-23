using System;
using Shouldly;
using ToolShare.Membership.CommunityRules;
using Xunit;

namespace ToolShare.Membership.Members;

public class EffectiveConcurrentLoanLimitTests
{
    private static Member CreateMemberAtRating(int rating)
    {
        var member = new Member(Guid.NewGuid(), Guid.NewGuid(), "Alice Example", "alice@example.com", DateTime.UtcNow, null);

        var delta = rating - member.CurrentRating;
        if (delta != 0)
        {
            member.ApplyOutcome(ReliabilityOutcomeType.ManualAdjustment, delta, occurrenceId: null, reason: "set up", DateTime.UtcNow, null);
        }

        return member;
    }

    [Fact]
    public void At_the_threshold_the_normal_limit_applies()
    {
        var rules = new CommunityRules.CommunityRules(Guid.NewGuid()); // LowRatingThreshold=50, ConcurrentLoanLimit=3, ReducedConcurrentLoanLimit=1
        var member = CreateMemberAtRating(rules.LowRatingThreshold);

        member.EffectiveConcurrentLoanLimit(rules).ShouldBe(rules.ConcurrentLoanLimit);
    }

    [Fact]
    public void Above_the_threshold_the_normal_limit_applies()
    {
        var rules = new CommunityRules.CommunityRules(Guid.NewGuid());
        var member = CreateMemberAtRating(rules.LowRatingThreshold + 10);

        member.EffectiveConcurrentLoanLimit(rules).ShouldBe(rules.ConcurrentLoanLimit);
    }

    [Fact]
    public void Below_the_threshold_the_reduced_limit_applies()
    {
        var rules = new CommunityRules.CommunityRules(Guid.NewGuid());
        var member = CreateMemberAtRating(rules.LowRatingThreshold - 10);

        member.EffectiveConcurrentLoanLimit(rules).ShouldBe(rules.ReducedConcurrentLoanLimit);
    }
}

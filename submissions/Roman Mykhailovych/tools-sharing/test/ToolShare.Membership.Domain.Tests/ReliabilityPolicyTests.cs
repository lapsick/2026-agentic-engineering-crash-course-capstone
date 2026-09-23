using System;
using Shouldly;
using ToolShare.Membership.CommunityRules;
using Xunit;

namespace ToolShare.Membership.Members;

public class ReliabilityPolicyTests
{
    private static CommunityRules.CommunityRules CreateRules()
    {
        // Defaults: OverduePenaltyPoints=10, DamagePenaltyPoints=20, CleanReturnRewardPoints=2.
        return new CommunityRules.CommunityRules(Guid.NewGuid());
    }

    [Fact]
    public void OverdueReturn_maps_to_negative_overdue_penalty()
    {
        var rules = CreateRules();

        ReliabilityPolicy.GetRawPoints(ReliabilityOutcomeType.OverdueReturn, rules)
            .ShouldBe(-rules.OverduePenaltyPoints);
    }

    [Fact]
    public void DamagedReturn_maps_to_negative_damage_penalty()
    {
        var rules = CreateRules();

        ReliabilityPolicy.GetRawPoints(ReliabilityOutcomeType.DamagedReturn, rules)
            .ShouldBe(-rules.DamagePenaltyPoints);
    }

    [Fact]
    public void CleanReturn_maps_to_positive_clean_return_reward()
    {
        var rules = CreateRules();

        ReliabilityPolicy.GetRawPoints(ReliabilityOutcomeType.CleanReturn, rules)
            .ShouldBe(rules.CleanReturnRewardPoints);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(-7)]
    public void ManualAdjustment_uses_the_caller_supplied_signed_value(int manualPoints)
    {
        var rules = CreateRules();

        ReliabilityPolicy.GetRawPoints(ReliabilityOutcomeType.ManualAdjustment, rules, manualPoints)
            .ShouldBe(manualPoints);
    }

    [Fact]
    public void ManualAdjustment_without_a_supplied_value_throws()
    {
        var rules = CreateRules();

        Should.Throw<ArgumentException>(() =>
            ReliabilityPolicy.GetRawPoints(ReliabilityOutcomeType.ManualAdjustment, rules));
    }
}

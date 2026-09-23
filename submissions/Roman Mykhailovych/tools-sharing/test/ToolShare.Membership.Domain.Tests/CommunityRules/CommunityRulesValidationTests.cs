using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Membership.CommunityRules;

public class CommunityRulesValidationTests
{
    private static CommunityRules CreateValid()
    {
        return new CommunityRules(Guid.NewGuid());
    }

    private static void UpdateWith(
        CommunityRules rules,
        int maxLoanTermDays = 14,
        int concurrentLoanLimit = 3,
        int lowRatingThreshold = 50,
        int reducedConcurrentLoanLimit = 1,
        int overduePenaltyPoints = 10,
        int damagePenaltyPoints = 20,
        int cleanReturnRewardPoints = 2,
        int waitlistOfferWindowHours = 24,
        int reminderLeadTimeDays = 2)
    {
        rules.Update(
            maxLoanTermDays,
            concurrentLoanLimit,
            lowRatingThreshold,
            reducedConcurrentLoanLimit,
            overduePenaltyPoints,
            damagePenaltyPoints,
            cleanReturnRewardPoints,
            waitlistOfferWindowHours,
            reminderLeadTimeDays);
    }

    [Fact]
    public void Valid_values_are_accepted()
    {
        var rules = CreateValid();

        UpdateWith(rules, maxLoanTermDays: 21, concurrentLoanLimit: 5, reducedConcurrentLoanLimit: 2);

        rules.MaxLoanTermDays.ShouldBe(21);
        rules.ConcurrentLoanLimit.ShouldBe(5);
        rules.ReducedConcurrentLoanLimit.ShouldBe(2);
    }

    [Fact]
    public void Reduced_limit_above_the_normal_limit_is_rejected_and_names_the_rule()
    {
        var rules = CreateValid();

        var exception = Should.Throw<BusinessException>(() =>
            UpdateWith(rules, concurrentLoanLimit: 3, reducedConcurrentLoanLimit: 4));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.InvalidRule);
        exception.Data["rule"].ShouldBe(nameof(CommunityRules.ReducedConcurrentLoanLimit));
    }

    [Fact]
    public void Reduced_limit_equal_to_the_normal_limit_is_accepted()
    {
        var rules = CreateValid();

        UpdateWith(rules, concurrentLoanLimit: 3, reducedConcurrentLoanLimit: 3);

        rules.ReducedConcurrentLoanLimit.ShouldBe(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_max_loan_term_is_rejected(int value)
    {
        var rules = CreateValid();

        var exception = Should.Throw<BusinessException>(() => UpdateWith(rules, maxLoanTermDays: value));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.InvalidRule);
        exception.Data["rule"].ShouldBe(nameof(CommunityRules.MaxLoanTermDays));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Out_of_range_low_rating_threshold_is_rejected(int value)
    {
        var rules = CreateValid();

        var exception = Should.Throw<BusinessException>(() => UpdateWith(rules, lowRatingThreshold: value));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.InvalidRule);
        exception.Data["rule"].ShouldBe(nameof(CommunityRules.LowRatingThreshold));
    }

    [Fact]
    public void Negative_penalty_points_are_rejected()
    {
        var rules = CreateValid();

        var exception = Should.Throw<BusinessException>(() => UpdateWith(rules, overduePenaltyPoints: -1));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.InvalidRule);
        exception.Data["rule"].ShouldBe(nameof(CommunityRules.OverduePenaltyPoints));
    }

    [Fact]
    public void A_fresh_instance_has_the_documented_defaults()
    {
        var rules = CreateValid();

        rules.MaxLoanTermDays.ShouldBe(CommunityRulesConsts.DefaultMaxLoanTermDays);
        rules.ConcurrentLoanLimit.ShouldBe(CommunityRulesConsts.DefaultConcurrentLoanLimit);
        rules.LowRatingThreshold.ShouldBe(CommunityRulesConsts.DefaultLowRatingThreshold);
        rules.ReducedConcurrentLoanLimit.ShouldBe(CommunityRulesConsts.DefaultReducedConcurrentLoanLimit);
        rules.OverduePenaltyPoints.ShouldBe(CommunityRulesConsts.DefaultOverduePenaltyPoints);
        rules.DamagePenaltyPoints.ShouldBe(CommunityRulesConsts.DefaultDamagePenaltyPoints);
        rules.CleanReturnRewardPoints.ShouldBe(CommunityRulesConsts.DefaultCleanReturnRewardPoints);
        rules.WaitlistOfferWindowHours.ShouldBe(CommunityRulesConsts.DefaultWaitlistOfferWindowHours);
        rules.ReminderLeadTimeDays.ShouldBe(CommunityRulesConsts.DefaultReminderLeadTimeDays);
    }
}

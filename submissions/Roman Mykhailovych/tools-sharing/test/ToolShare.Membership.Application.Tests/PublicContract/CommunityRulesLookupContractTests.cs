using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.CommunityRules;

/// <summary>Every rule value is readable through the public, read-only lookup contract (never <c>null</c> — the seeder guarantees the row exists, CRR-02).</summary>
public class CommunityRulesLookupContractTests : MembershipAuthorizationTestBase
{
    private readonly ICommunityRulesLookupAppService _rulesLookupAppService;

    public CommunityRulesLookupContractTests()
    {
        _rulesLookupAppService = GetRequiredService<ICommunityRulesLookupAppService>();
    }

    [Fact]
    public async Task Every_rule_value_is_readable_on_a_fresh_installation()
    {
        var rules = await _rulesLookupAppService.GetAsync();

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

    [Fact]
    public async Task GetAsync_is_read_only_and_available_to_any_active_member()
    {
        using (AsMemberWithNoGrants())
        {
            var rules = await _rulesLookupAppService.GetAsync();

            rules.ShouldNotBeNull();
        }
    }
}

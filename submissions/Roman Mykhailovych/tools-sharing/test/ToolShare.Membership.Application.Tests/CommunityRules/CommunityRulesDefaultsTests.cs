using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.CommunityRules;

/// <summary>SC-007: every rule has its documented default after seeding, on a fresh installation.</summary>
public class CommunityRulesDefaultsTests : MembershipAuthorizationTestBase
{
    private readonly ICommunityRulesAppService _communityRulesAppService;

    public CommunityRulesDefaultsTests()
    {
        _communityRulesAppService = GetRequiredService<ICommunityRulesAppService>();
    }

    [Fact]
    public async Task Every_rule_has_its_documented_default_after_seeding()
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var rules = await _communityRulesAppService.GetAsync();

            rules.MaxLoanTermDays.ShouldBe(CommunityRulesConsts.DefaultMaxLoanTermDays);
            rules.ConcurrentLoanLimit.ShouldBe(CommunityRulesConsts.DefaultConcurrentLoanLimit);
            rules.LowRatingThreshold.ShouldBe(CommunityRulesConsts.DefaultLowRatingThreshold);
            rules.ReducedConcurrentLoanLimit.ShouldBe(CommunityRulesConsts.DefaultReducedConcurrentLoanLimit);
            rules.OverduePenaltyPoints.ShouldBe(CommunityRulesConsts.DefaultOverduePenaltyPoints);
            rules.DamagePenaltyPoints.ShouldBe(CommunityRulesConsts.DefaultDamagePenaltyPoints);
            rules.CleanReturnRewardPoints.ShouldBe(CommunityRulesConsts.DefaultCleanReturnRewardPoints);
            rules.WaitlistOfferWindowHours.ShouldBe(CommunityRulesConsts.DefaultWaitlistOfferWindowHours);
            rules.ReminderLeadTimeDays.ShouldBe(CommunityRulesConsts.DefaultReminderLeadTimeDays);

            // Nobody has changed the rules since the seeder inserted them.
            rules.LastChangedAt.ShouldBeNull();
            rules.LastChangedByUserId.ShouldBeNull();
            rules.LastChangedByDisplayName.ShouldBeNull();
        }
    }
}

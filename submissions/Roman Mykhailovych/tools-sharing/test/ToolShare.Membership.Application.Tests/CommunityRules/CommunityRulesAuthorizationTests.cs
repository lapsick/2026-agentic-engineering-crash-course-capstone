using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Membership.CommunityRules;

/// <summary>FR-013: any active member may read the community rules; only an Administrator may write them.</summary>
public class CommunityRulesAuthorizationTests : MembershipAuthorizationTestBase
{
    private readonly ICommunityRulesAppService _communityRulesAppService;

    public CommunityRulesAuthorizationTests()
    {
        _communityRulesAppService = GetRequiredService<ICommunityRulesAppService>();
    }

    private static UpdateCommunityRulesDto AnyValidInput(string concurrencyStamp)
    {
        return new UpdateCommunityRulesDto
        {
            MaxLoanTermDays = 14,
            ConcurrentLoanLimit = 3,
            LowRatingThreshold = 50,
            ReducedConcurrentLoanLimit = 1,
            OverduePenaltyPoints = 10,
            DamagePenaltyPoints = 20,
            CleanReturnRewardPoints = 2,
            WaitlistOfferWindowHours = 24,
            ReminderLeadTimeDays = 2,
            ConcurrencyStamp = concurrencyStamp
        };
    }

    [Fact]
    public async Task Reading_the_rules_succeeds_for_any_active_member_without_membership_grants()
    {
        using (AsMemberWithNoGrants())
        {
            await Should.NotThrowAsync(() => _communityRulesAppService.GetAsync());
        }
    }

    [Fact]
    public async Task Reading_the_rules_is_denied_for_an_anonymous_principal()
    {
        using (AsAnonymous())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _communityRulesAppService.GetAsync());
        }
    }

    [Fact]
    public async Task Reading_the_rules_is_denied_for_an_authenticated_non_member()
    {
        using (AsAuthenticatedNonMember())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _communityRulesAppService.GetAsync());
        }
    }

    [Fact]
    public async Task Updating_the_rules_is_denied_for_an_active_member_without_the_rules_edit_grant()
    {
        using (AsMemberWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _communityRulesAppService.UpdateAsync(AnyValidInput("irrelevant-stamp")));
        }
    }

    [Fact]
    public async Task Updating_the_rules_is_denied_for_an_anonymous_principal()
    {
        using (AsAnonymous())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _communityRulesAppService.UpdateAsync(AnyValidInput("irrelevant-stamp")));
        }
    }

    [Fact]
    public async Task Updating_the_rules_succeeds_for_an_administrator()
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var current = await _communityRulesAppService.GetAsync();

            await Should.NotThrowAsync(() => _communityRulesAppService.UpdateAsync(AnyValidInput(current.ConcurrencyStamp)));
        }
    }
}

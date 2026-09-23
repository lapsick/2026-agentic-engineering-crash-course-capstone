using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace ToolShare.Membership.CommunityRules;

/// <summary>FR-031, US2 scenario 7: the second of two concurrent saves is rejected.</summary>
public class CommunityRulesConcurrencyTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task The_second_of_two_concurrent_saves_is_rejected()
    {
        var communityRulesAppService = GetRequiredService<ICommunityRulesAppService>();
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            var current = await communityRulesAppService.GetAsync();

            var firstEditorInput = new UpdateCommunityRulesDto
            {
                MaxLoanTermDays = 20,
                ConcurrentLoanLimit = current.ConcurrentLoanLimit,
                LowRatingThreshold = current.LowRatingThreshold,
                ReducedConcurrentLoanLimit = current.ReducedConcurrentLoanLimit,
                OverduePenaltyPoints = current.OverduePenaltyPoints,
                DamagePenaltyPoints = current.DamagePenaltyPoints,
                CleanReturnRewardPoints = current.CleanReturnRewardPoints,
                WaitlistOfferWindowHours = current.WaitlistOfferWindowHours,
                ReminderLeadTimeDays = current.ReminderLeadTimeDays,
                ConcurrencyStamp = current.ConcurrencyStamp
            };

            await communityRulesAppService.UpdateAsync(firstEditorInput);

            // A second editor who loaded the rules before the first save
            // still holds the now-stale ConcurrencyStamp.
            var secondEditorInput = new UpdateCommunityRulesDto
            {
                MaxLoanTermDays = 25,
                ConcurrentLoanLimit = current.ConcurrentLoanLimit,
                LowRatingThreshold = current.LowRatingThreshold,
                ReducedConcurrentLoanLimit = current.ReducedConcurrentLoanLimit,
                OverduePenaltyPoints = current.OverduePenaltyPoints,
                DamagePenaltyPoints = current.DamagePenaltyPoints,
                CleanReturnRewardPoints = current.CleanReturnRewardPoints,
                WaitlistOfferWindowHours = current.WaitlistOfferWindowHours,
                ReminderLeadTimeDays = current.ReminderLeadTimeDays,
                ConcurrencyStamp = current.ConcurrencyStamp
            };

            await Should.ThrowAsync<AbpDbConcurrencyException>(() => communityRulesAppService.UpdateAsync(secondEditorInput));
        }
    }
}

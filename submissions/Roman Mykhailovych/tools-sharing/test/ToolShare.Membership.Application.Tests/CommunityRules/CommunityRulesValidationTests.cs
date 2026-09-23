using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Membership.CommunityRules;

/// <summary>
/// FR-014, through the app service: per-field ranges are caught by data
/// annotations on <see cref="UpdateCommunityRulesDto"/>, the cross-field rule
/// (<c>ReducedConcurrentLoanLimit &lt;= ConcurrentLoanLimit</c>, CRR-01) is
/// delegated to and enforced by the domain, and a valid update persists.
/// </summary>
public class CommunityRulesValidationTests : MembershipAuthorizationTestBase
{
    private readonly ICommunityRulesAppService _communityRulesAppService;

    public CommunityRulesValidationTests()
    {
        _communityRulesAppService = GetRequiredService<ICommunityRulesAppService>();
    }

    private static UpdateCommunityRulesDto ValidInputFrom(CommunityRulesDetailDto current)
    {
        return new UpdateCommunityRulesDto
        {
            MaxLoanTermDays = current.MaxLoanTermDays,
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
    }

    [Fact]
    public async Task A_reduced_limit_above_the_normal_limit_is_rejected_naming_the_field()
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var current = await _communityRulesAppService.GetAsync();

            var input = ValidInputFrom(current);
            input.ConcurrentLoanLimit = 3;
            input.ReducedConcurrentLoanLimit = 5;

            var exception = await Should.ThrowAsync<BusinessException>(() => _communityRulesAppService.UpdateAsync(input));

            exception.Code.ShouldBe(MembershipDomainErrorCodes.InvalidRule);
            exception.Data["rule"].ShouldBe(nameof(CommunityRules.ReducedConcurrentLoanLimit));
        }
    }

    [Fact]
    public async Task A_reduced_limit_equal_to_the_normal_limit_is_accepted()
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var current = await _communityRulesAppService.GetAsync();

            var input = ValidInputFrom(current);
            input.ConcurrentLoanLimit = 4;
            input.ReducedConcurrentLoanLimit = 4;

            var updated = await _communityRulesAppService.UpdateAsync(input);

            updated.ConcurrentLoanLimit.ShouldBe(4);
            updated.ReducedConcurrentLoanLimit.ShouldBe(4);
        }
    }

    [Fact]
    public async Task Changing_two_values_persists_both()
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var current = await _communityRulesAppService.GetAsync();

            var input = ValidInputFrom(current);
            input.MaxLoanTermDays = 21;
            input.OverduePenaltyPoints = 15;

            var updated = await _communityRulesAppService.UpdateAsync(input);

            updated.MaxLoanTermDays.ShouldBe(21);
            updated.OverduePenaltyPoints.ShouldBe(15);

            var reloaded = await _communityRulesAppService.GetAsync();
            reloaded.MaxLoanTermDays.ShouldBe(21);
            reloaded.OverduePenaltyPoints.ShouldBe(15);
        }
    }
}

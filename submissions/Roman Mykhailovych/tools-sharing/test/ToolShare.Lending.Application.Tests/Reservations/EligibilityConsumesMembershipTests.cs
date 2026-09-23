using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// FR-018, spec.md US1 scenarios 4-5: changing a member's rating via
/// Membership's own admin surface measurably changes the effective
/// concurrent-loan limit ReservationAppService.CreateAsync sees — proving the
/// check is live against Membership's own standing, never cached or
/// locally recomputed by Lending.
/// </summary>
public class EligibilityConsumesMembershipTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly IMemberStandingAppService _memberStandingAppService;
    private readonly IMemberAppService _memberAppService;
    private readonly ICommunityRulesLookupAppService _communityRulesLookupAppService;

    public EligibilityConsumesMembershipTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _memberStandingAppService = GetRequiredService<IMemberStandingAppService>();
        _memberAppService = GetRequiredService<IMemberAppService>();
        _communityRulesLookupAppService = GetRequiredService<ICommunityRulesLookupAppService>();
    }

    [Fact]
    public async Task Lowering_a_members_rating_below_the_threshold_reduces_their_live_concurrent_loan_limit()
    {
        var standing = await _memberStandingAppService.GetByIdentityUserIdAsync(LendingTestPrincipals.NoGrantsUserId);
        var memberId = standing.MemberId!.Value;
        var rules = await _communityRulesLookupAppService.GetAsync();

        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var member = await _memberAppService.GetAsync(memberId);
            var pointsToDrop = member.CurrentRating - rules.LowRatingThreshold + 1;

            await _memberAppService.AdjustRatingAsync(memberId, new AdjustMemberRatingDto
            {
                Points = -pointsToDrop,
                Reason = "Test: force below the low-rating threshold",
                ConcurrencyStamp = member.ConcurrencyStamp
            });
        }

        var instanceIds = new List<Guid>();
        for (var i = 0; i < rules.ReducedConcurrentLoanLimit; i++)
        {
            var (_, _, instance) = await SeedCatalogDataAsync();
            instanceIds.Add(instance.Id);
        }

        var (_, _, extraInstance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        using (AsMemberWithNoGrants())
        {
            foreach (var instanceId in instanceIds)
            {
                await _reservationAppService.CreateAsync(new CreateReservationDto
                {
                    ToolInstanceId = instanceId,
                    StartDate = start,
                    EndDate = start.AddDays(2)
                });
            }

            var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = extraInstance.Id,
                StartDate = start,
                EndDate = start.AddDays(2)
            }));

            exception.Code.ShouldBe(LendingDomainErrorCodes.ConcurrentLoanLimitReached);
        }
    }
}

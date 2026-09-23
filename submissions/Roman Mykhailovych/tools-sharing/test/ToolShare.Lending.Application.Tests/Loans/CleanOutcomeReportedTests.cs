using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>FR-020: an on-time, undamaged return reports CleanReturn and raises the rating by the configured reward.</summary>
public class CleanOutcomeReportedTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMemberAppService _memberAppService;
    private readonly ICommunityRulesLookupAppService _communityRulesLookupAppService;

    public CleanOutcomeReportedTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _memberAppService = GetRequiredService<IMemberAppService>();
        _communityRulesLookupAppService = GetRequiredService<ICommunityRulesLookupAppService>();
    }

    [Fact]
    public async Task An_on_time_undamaged_return_reports_CleanReturn_and_raises_the_rating()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        Guid memberId;
        LoanDto loan;
        using (AsMemberWithNoGrants())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(5)
            });
            memberId = reservation.MemberId;

            using (AsLibrarian())
            {
                loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
            }
        }

        var rules = await _communityRulesLookupAppService.GetAsync();

        // A fresh member starts at the rating ceiling (MembershipDomainSharedConsts.MaxRating) —
        // without first making room below it, the reward would be clamped to
        // a no-op and this test would prove nothing.
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var member = await _memberAppService.GetAsync(memberId);
            await _memberAppService.AdjustRatingAsync(memberId, new AdjustMemberRatingDto
            {
                Points = -10,
                Reason = "Test setup: make room below the rating ceiling",
                ConcurrencyStamp = member.ConcurrencyStamp
            });
        }

        var before = await _memberAppService.GetAsync(memberId);

        LoanDto returned;
        using (AsLibrarian())
        {
            returned = await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good });
        }

        returned.IsOverdue.ShouldBeFalse();

        var after = await _memberAppService.GetAsync(memberId);
        after.CurrentRating.ShouldBe(before.CurrentRating + rules.CleanReturnRewardPoints);
    }
}

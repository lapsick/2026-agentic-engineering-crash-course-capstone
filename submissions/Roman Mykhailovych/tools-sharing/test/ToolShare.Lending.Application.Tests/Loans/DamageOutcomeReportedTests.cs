using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>FR-020: a worsened, on-time return reports DamagedReturn and lowers the rating by the configured damage penalty.</summary>
public class DamageOutcomeReportedTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMemberAppService _memberAppService;
    private readonly ICommunityRulesLookupAppService _communityRulesLookupAppService;

    public DamageOutcomeReportedTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _memberAppService = GetRequiredService<IMemberAppService>();
        _communityRulesLookupAppService = GetRequiredService<ICommunityRulesLookupAppService>();
    }

    [Fact]
    public async Task A_worsened_on_time_return_reports_DamagedReturn_and_lowers_the_rating()
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
        var before = await _memberAppService.GetAsync(memberId);

        LoanDto returned;
        using (AsLibrarian())
        {
            returned = await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Worn });
        }

        returned.IsOverdue.ShouldBeFalse();

        var after = await _memberAppService.GetAsync(memberId);
        after.CurrentRating.ShouldBe(before.CurrentRating - rules.DamagePenaltyPoints);
    }
}

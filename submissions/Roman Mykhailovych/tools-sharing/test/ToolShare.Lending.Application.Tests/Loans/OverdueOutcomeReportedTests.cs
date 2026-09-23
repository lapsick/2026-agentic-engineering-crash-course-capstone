using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>
/// FR-020: a late return reports OverdueReturn with Loan.Id as OccurrenceId,
/// and the member's rating drops by the configured overdue penalty. The
/// reservation's own EndDate is set in the past so returning "now" is late —
/// LOAN-03's own lateness check, not a background worker, is what marks it
/// overdue (the marking worker is US5's concern).
/// </summary>
public class OverdueOutcomeReportedTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMemberAppService _memberAppService;
    private readonly ICommunityRulesLookupAppService _communityRulesLookupAppService;

    public OverdueOutcomeReportedTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _memberAppService = GetRequiredService<IMemberAppService>();
        _communityRulesLookupAppService = GetRequiredService<ICommunityRulesLookupAppService>();
    }

    [Fact]
    public async Task A_late_return_reports_OverdueReturn_and_lowers_the_rating()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10);
        var end = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        Guid memberId;
        LoanDto loan;
        using (AsMemberWithNoGrants())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = end
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
            returned = await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good });
        }

        returned.IsOverdue.ShouldBeTrue();

        var after = await _memberAppService.GetAsync(memberId);
        after.CurrentRating.ShouldBe(before.CurrentRating - rules.OverduePenaltyPoints);
    }
}

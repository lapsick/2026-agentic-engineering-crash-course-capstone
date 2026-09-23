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
/// FR-020's "late AND damaged" edge case: both outcomes are reported against
/// the same Loan.Id, relying on Membership's composite (OccurrenceId,
/// OutcomeType) idempotency key to accept both without collision.
/// </summary>
public class BothOutcomesReportedTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMemberAppService _memberAppService;
    private readonly ICommunityRulesLookupAppService _communityRulesLookupAppService;

    public BothOutcomesReportedTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _memberAppService = GetRequiredService<IMemberAppService>();
        _communityRulesLookupAppService = GetRequiredService<ICommunityRulesLookupAppService>();
    }

    [Fact]
    public async Task A_late_and_worsened_return_reports_both_outcomes()
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
            returned = await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Worn });
        }

        returned.IsOverdue.ShouldBeTrue();
        returned.ReturnedCondition.ShouldBe(ToolCondition.Worn);

        var after = await _memberAppService.GetAsync(memberId);
        after.CurrentRating.ShouldBe(before.CurrentRating - rules.OverduePenaltyPoints - rules.DamagePenaltyPoints);
    }
}

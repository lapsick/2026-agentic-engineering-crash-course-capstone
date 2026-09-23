using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>LOAN-05: Loan.ReliabilityReportedAt prevents a retried close from reporting a second time.</summary>
public class ReliabilityReportIdempotenceTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMemberAppService _memberAppService;

    public ReliabilityReportIdempotenceTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _memberAppService = GetRequiredService<IMemberAppService>();
    }

    [Fact]
    public async Task Calling_ReturnAsync_again_on_an_already_closed_loan_does_not_report_a_second_time()
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

        using (AsLibrarian())
        {
            await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good });
        }

        var afterFirstClose = await _memberAppService.GetAsync(memberId);

        // A retried close (e.g. after a crash) — same loan, condition value
        // is irrelevant since ReturnedAt is already set, so the Catalog-facing
        // half is skipped entirely and only the (already-done) reliability
        // half would be reachable, but ReliabilityReportedAt gates that too.
        using (AsLibrarian())
        {
            await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Damaged });
        }

        var afterSecondClose = await _memberAppService.GetAsync(memberId);

        afterSecondClose.CurrentRating.ShouldBe(afterFirstClose.CurrentRating);
    }
}

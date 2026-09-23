using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>FR-011: a return before the planned return date is accepted and never marked overdue.</summary>
public class EarlyReturnTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;

    public EarlyReturnTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
    }

    [Fact]
    public async Task Returning_before_the_planned_return_date_is_accepted_and_not_overdue()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        ReservationDto reservation;
        using (AsMemberWithNoGrants())
        {
            reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(10)
            });
        }

        using (AsLibrarian())
        {
            var loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });

            var returned = await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good });

            returned.ReturnedAt.ShouldNotBeNull();
            returned.IsOverdue.ShouldBeFalse();
        }
    }
}

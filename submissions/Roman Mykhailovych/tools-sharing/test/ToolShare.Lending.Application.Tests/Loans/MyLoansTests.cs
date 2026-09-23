using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>FR-030: a member sees only their own loans via IMyLendingAppService.GetMyLoansAsync.</summary>
public class MyLoansTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMyLendingAppService _myLendingAppService;

    public MyLoansTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _myLendingAppService = GetRequiredService<IMyLendingAppService>();
    }

    [Fact]
    public async Task A_member_sees_only_their_own_loans()
    {
        var (_, _, instanceA) = await SeedCatalogDataAsync();
        var (_, _, instanceB) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        ReservationDto reservationForNoGrantsMember;
        using (AsMemberWithNoGrants())
        {
            reservationForNoGrantsMember = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instanceA.Id,
                StartDate = start,
                EndDate = start.AddDays(3)
            });
        }

        ReservationDto reservationForLibrarian;
        using (AsLibrarian())
        {
            reservationForLibrarian = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instanceB.Id,
                StartDate = start,
                EndDate = start.AddDays(3)
            });
        }

        using (AsLibrarian())
        {
            await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservationForNoGrantsMember.Id });
            await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservationForLibrarian.Id });
        }

        using (AsLibrarian())
        {
            var myLoans = await _myLendingAppService.GetMyLoansAsync();

            myLoans.Count.ShouldBe(1);
            myLoans[0].ReservationId.ShouldBe(reservationForLibrarian.Id);
            myLoans[0].ToolInstanceId.ShouldBe(instanceB.Id);
        }
    }
}

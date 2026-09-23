using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Lending.Authorization;

/// <summary>FR-029: checkout/return require Lending.Loans.Checkout/.Return, denied for a plain Member.</summary>
public class LoanAuthorizationTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;

    public LoanAuthorizationTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
    }

    [Fact]
    public async Task A_member_with_no_Lending_grants_cannot_check_out()
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
                EndDate = start.AddDays(3)
            });
        }

        using (AsMemberWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id }));
        }
    }

    [Fact]
    public async Task A_member_with_no_Lending_grants_cannot_record_a_return()
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
                EndDate = start.AddDays(3)
            });
        }

        LoanDto loan;
        using (AsLibrarian())
        {
            loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
        }

        using (AsMemberWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good }));
        }
    }
}

using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Reservations;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>FR-010, LOAN-01, LOAN-02, LOAN-06.</summary>
public class CheckOutTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;

    public CheckOutTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _toolInstanceLookupAppService = GetRequiredService<IToolInstanceLookupAppService>();
    }

    [Fact]
    public async Task Checking_out_against_an_active_reservation_marks_the_instance_on_loan()
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

        loan.ReservationId.ShouldBe(reservation.Id);
        loan.ToolInstanceId.ShouldBe(instance.Id);
        loan.ReturnedAt.ShouldBeNull();

        var lookup = await _toolInstanceLookupAppService.FindAsync(instance.Id);
        lookup!.CirculationState.ShouldBe(ToolInstanceCirculationState.OnLoan);
        lookup.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Checking_out_without_a_matching_active_reservation_is_rejected()
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

            await _reservationAppService.CancelAsync(reservation.Id);
        }

        using (AsLibrarian())
        {
            var exception = await Should.ThrowAsync<BusinessException>(() => _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id }));
            exception.Code.ShouldBe(LendingDomainErrorCodes.NoMatchingActiveReservation);
        }
    }

    [Fact]
    public async Task Checking_out_against_an_instance_that_became_unavailable_is_rejected()
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

        using (AsLibrarian())
        {
            await ToolInstanceAppService.RetireAsync(instance.Id, new RetireToolInstanceDto
            {
                Reason = "retired mid-flow",
                ConcurrencyStamp = instance.ConcurrencyStamp
            });

            var exception = await Should.ThrowAsync<BusinessException>(() => _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id }));
            exception.Code.ShouldBe(LendingDomainErrorCodes.InstanceUnavailable);
        }
    }
}

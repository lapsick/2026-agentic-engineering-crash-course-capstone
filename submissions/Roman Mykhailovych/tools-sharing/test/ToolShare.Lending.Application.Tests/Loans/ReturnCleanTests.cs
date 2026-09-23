using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>FR-012: a clean return closes the loan, frees the instance, and offers the waitlist if one exists.</summary>
public class ReturnCleanTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;

    public ReturnCleanTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _toolInstanceLookupAppService = GetRequiredService<IToolInstanceLookupAppService>();
    }

    [Fact]
    public async Task Returning_in_the_same_condition_closes_the_loan_and_frees_the_instance()
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
            var loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });

            var returned = await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good });

            returned.ReturnedAt.ShouldNotBeNull();
            returned.ReturnedCondition.ShouldBe(ToolCondition.Good);
        }

        var lookup = await _toolInstanceLookupAppService.FindAsync(instance.Id);
        lookup!.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        lookup.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Returning_cleanly_offers_the_waitlist_when_one_exists()
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

        WaitlistEntryDto waitlistEntry;
        using (AsLibrarian())
        {
            waitlistEntry = await _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id });
        }

        using (AsLibrarian())
        {
            var loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
            await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good });
        }

        var waitlistEntryRepository = GetRequiredService<IWaitlistEntryRepository>();
        var offered = await waitlistEntryRepository.GetAsync(waitlistEntry.Id);
        offered.OfferState.ShouldBe(WaitlistOfferState.Offered);
    }
}

using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>WL-03: closing a maintenance request offers the instance to the earliest waiting member.</summary>
public class MaintenanceClosingTriggersWaitlistTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;

    public MaintenanceClosingTriggersWaitlistTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _maintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
        _waitlistEntryRepository = GetRequiredService<IWaitlistEntryRepository>();
    }

    [Fact]
    public async Task Closing_a_request_offers_the_earliest_waiting_member()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        using (AsMemberWithNoGrants())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(2)
            });

            using (AsLibrarian())
            {
                var loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
                await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Damaged });
            }
        }

        WaitlistEntryDto waitlistEntry;
        using (AsLibrarian())
        {
            waitlistEntry = await _reservationAppService.JoinWaitlistAsync(new JoinWaitlistDto { ToolInstanceId = instance.Id });
        }

        var opened = await _maintenanceRequestRepository.GetOpenForInstanceAsync(instance.Id);
        opened.ShouldNotBeNull();

        using (AsLibrarian())
        {
            await _maintenanceRequestAppService.CloseAsync(opened!.Id, new CloseMaintenanceRequestDto { Cost = 10m });
        }

        var offered = await _waitlistEntryRepository.GetAsync(waitlistEntry.Id);
        offered.OfferState.ShouldBe(WaitlistOfferState.Offered);
    }
}

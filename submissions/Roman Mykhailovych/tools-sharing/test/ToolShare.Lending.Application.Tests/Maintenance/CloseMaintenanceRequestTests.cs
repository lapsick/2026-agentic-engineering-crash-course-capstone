using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>FR-016, MAINT-02: closing an open maintenance request restores availability; the cost is retained (FR-017).</summary>
public class CloseMaintenanceRequestTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;

    public CloseMaintenanceRequestTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _maintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
        _toolInstanceLookupAppService = GetRequiredService<IToolInstanceLookupAppService>();
    }

    [Fact]
    public async Task Closing_with_a_stated_cost_restores_availability_and_retains_the_cost()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        MaintenanceRequestDto? closed = null;

        using (AsMemberWithNoGrants())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(3)
            });

            using (AsLibrarian())
            {
                var loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });

                // Worn (not Damaged): still a genuinely worsened return
                // (opens a maintenance request) but — unlike Damaged, which
                // permanently excludes the instance from availability
                // regardless of circulation state (ToolInstance.IsAvailable) —
                // closing the request should actually restore availability.
                await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Worn });
            }
        }

        var opened = await _maintenanceRequestRepository.GetOpenForInstanceAsync(instance.Id);
        opened.ShouldNotBeNull();

        using (AsLibrarian())
        {
            closed = await _maintenanceRequestAppService.CloseAsync(opened!.Id, new CloseMaintenanceRequestDto { Cost = 42.50m });
        }

        closed.Status.ShouldBe(MaintenanceRequestStatus.Closed);
        closed.Cost.ShouldBe(42.50m);
        closed.ClosedAt.ShouldNotBeNull();

        var lookup = await _toolInstanceLookupAppService.FindAsync(instance.Id);
        lookup!.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        lookup.IsAvailable.ShouldBeTrue();
    }
}

using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Loans;

/// <summary>FR-012, FR-014, LOAN-04: a worsened return closes the loan, opens exactly one maintenance request, and keeps the instance unavailable.</summary>
public class ReturnWorsenedTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;

    public ReturnWorsenedTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _toolInstanceLookupAppService = GetRequiredService<IToolInstanceLookupAppService>();
        _maintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
    }

    [Fact]
    public async Task Returning_in_a_worse_condition_opens_a_maintenance_request_and_keeps_the_instance_unavailable()
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

            var returned = await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Damaged });

            returned.ReturnedAt.ShouldNotBeNull();
            returned.ReturnedCondition.ShouldBe(ToolCondition.Damaged);
        }

        var maintenanceRequest = await _maintenanceRequestRepository.GetOpenForInstanceAsync(instance.Id);
        maintenanceRequest.ShouldNotBeNull();
        maintenanceRequest!.TriggeringLoanId.ShouldBe(loan.Id);
        maintenanceRequest.Status.ShouldBe(MaintenanceRequestStatus.Open);

        var lookup = await _toolInstanceLookupAppService.FindAsync(instance.Id);
        lookup!.CirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
        lookup.IsAvailable.ShouldBeFalse();
    }
}

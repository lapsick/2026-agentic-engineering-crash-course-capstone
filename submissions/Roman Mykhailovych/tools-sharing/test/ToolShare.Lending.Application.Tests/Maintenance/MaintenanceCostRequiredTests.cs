using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using Volo.Abp.Validation;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// MAINT-02. The DTO's <c>Cost</c> is a non-nullable <c>decimal</c> — the
/// domain's own "omitted vs explicit zero" distinction (<see cref="MaintenanceRequest.Close"/>)
/// is exercised at the domain-test level; this level exercises what the DTO
/// boundary can actually express: a negative cost is rejected by validation,
/// and an explicit zero is a legitimate, distinct, accepted cost.
/// </summary>
public class MaintenanceCostRequiredTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;

    public MaintenanceCostRequiredTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _maintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
    }

    [Fact]
    public async Task Closing_with_a_negative_cost_is_rejected()
    {
        var requestId = await OpenMaintenanceRequestAsync();

        using (AsLibrarian())
        {
            await Should.ThrowAsync<AbpValidationException>(() => _maintenanceRequestAppService.CloseAsync(requestId, new CloseMaintenanceRequestDto { Cost = -1m }));
        }
    }

    [Fact]
    public async Task Closing_with_an_explicit_zero_cost_is_accepted()
    {
        var requestId = await OpenMaintenanceRequestAsync();

        using (AsLibrarian())
        {
            var closed = await _maintenanceRequestAppService.CloseAsync(requestId, new CloseMaintenanceRequestDto { Cost = 0m });

            closed.Status.ShouldBe(MaintenanceRequestStatus.Closed);
            closed.Cost.ShouldBe(0m);
        }
    }

    private async Task<Guid> OpenMaintenanceRequestAsync()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

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
                await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Damaged });
            }
        }

        var opened = await _maintenanceRequestRepository.GetOpenForInstanceAsync(instance.Id);
        return opened!.Id;
    }
}

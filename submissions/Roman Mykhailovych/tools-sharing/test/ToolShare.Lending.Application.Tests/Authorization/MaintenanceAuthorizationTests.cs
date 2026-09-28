using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Reservations;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Lending.Authorization;

/// <summary>FR-029: closing a maintenance request requires Lending.Maintenance.Close, denied for a plain Member.</summary>
public class MaintenanceAuthorizationTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;

    public MaintenanceAuthorizationTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _maintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
    }

    [Fact]
    public async Task A_member_with_no_Lending_grants_cannot_close_a_maintenance_request()
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

        var opened = await _maintenanceRequestRepository.GetOpenForInstanceAsync(instance.Id);
        opened.ShouldNotBeNull();

        using (AsMemberWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _maintenanceRequestAppService.CloseAsync(opened!.Id, new CloseMaintenanceRequestDto { Cost = 10m }));
        }
    }

    /// <summary>008 FR-020: reporting out-of-band requires Lending.Maintenance.Report — granted to Librarian.</summary>
    [Fact]
    public async Task A_Librarian_may_report_maintenance_out_of_band()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();

        using (AsLibrarian())
        {
            await Should.NotThrowAsync(() => _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instance.Id,
                ObservedCondition = ToolCondition.Good,
                Reason = "Frayed cord"
            }));
        }
    }

    /// <summary>008 FR-020 / FR-021: a plain member has no way to report maintenance.</summary>
    [Fact]
    public async Task A_member_with_no_Lending_grants_cannot_report_maintenance()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();

        using (AsMemberWithNoGrants())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instance.Id,
                ObservedCondition = ToolCondition.Good,
                Reason = "Frayed cord"
            }));
        }
    }
}

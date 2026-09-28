using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 US4 (FR-015, FR-017): the open maintenance queue labels each request by
/// origin — a return-triggered request with its loan, an out-of-band one with
/// who reported it, why, and the condition observed.
/// </summary>
public class MaintenanceQueueOriginTests : LendingAuthorizationTestBase
{
    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IMemberStandingAppService _memberStandingAppService;

    public MaintenanceQueueOriginTests()
    {
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _memberStandingAppService = GetRequiredService<IMemberStandingAppService>();
    }

    [Fact]
    public async Task The_open_queue_shows_each_request_with_its_origin_details()
    {
        var (_, _, returned) = await SeedCatalogDataAsync();
        var (_, _, reported) = await SeedCatalogDataAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        LoanDto loan;
        using (AsMemberWithNoGrants())
        {
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = returned.Id,
                StartDate = today,
                EndDate = today.AddDays(2)
            });

            using (AsLibrarian())
            {
                loan = await _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id });
                await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Worn });
            }
        }

        using (AsLibrarian())
        {
            await _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = reported.Id,
                ObservedCondition = ToolCondition.Damaged,
                Reason = "Motor sparks"
            });
        }

        var librarian = await _memberStandingAppService.GetByIdentityUserIdAsync(LendingTestPrincipals.LibrarianUserId);

        using (AsLibrarian())
        {
            var open = (await _maintenanceRequestAppService.GetOpenListAsync()).Items;

            var fromReturn = open.Single(r => r.ToolInstanceId == returned.Id);
            fromReturn.Origin.ShouldBe(MaintenanceRequestOrigin.ReturnTriggered);
            fromReturn.TriggeringLoanId.ShouldBe(loan.Id);
            fromReturn.ReportedByMemberId.ShouldBeNull();
            fromReturn.ReportReason.ShouldBeNull();
            fromReturn.ObservedCondition.ShouldBeNull();

            var fromReport = open.Single(r => r.ToolInstanceId == reported.Id);
            fromReport.Origin.ShouldBe(MaintenanceRequestOrigin.OutOfBand);
            fromReport.TriggeringLoanId.ShouldBeNull();
            fromReport.ReportedByMemberId.ShouldBe(librarian.MemberId);
            fromReport.ReportReason.ShouldBe("Motor sparks");
            fromReport.ObservedCondition.ShouldBe(ToolCondition.Damaged);
        }
    }
}

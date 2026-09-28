using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.Members;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 US1 (FR-001, FR-007, FR-011, FR-012, FR-013, FR-015): a Librarian sends
/// an in-circulation, not-on-loan instance to maintenance; it becomes
/// unavailable, appears in the open queue, closes exactly as a
/// return-triggered request does, and no member's reliability rating moves.
/// </summary>
public class ReportOutOfBandTests : LendingAuthorizationTestBase
{
    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;
    private readonly IMemberStandingAppService _memberStandingAppService;

    public ReportOutOfBandTests()
    {
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _toolInstanceLookupAppService = GetRequiredService<IToolInstanceLookupAppService>();
        _memberStandingAppService = GetRequiredService<IMemberStandingAppService>();
    }

    [Fact]
    public async Task Reporting_an_in_circulation_instance_opens_an_out_of_band_request_attributed_to_the_reporting_member()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var librarian = await _memberStandingAppService.GetByIdentityUserIdAsync(LendingTestPrincipals.LibrarianUserId);

        MaintenanceRequestDto report;
        using (AsLibrarian())
        {
            report = await _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instance.Id,
                ObservedCondition = ToolCondition.Worn,
                Reason = "  Cracked blade guard  "
            });
        }

        report.Origin.ShouldBe(MaintenanceRequestOrigin.OutOfBand);
        report.Status.ShouldBe(MaintenanceRequestStatus.Open);
        report.ToolInstanceId.ShouldBe(instance.Id);
        report.TriggeringLoanId.ShouldBeNull();
        report.ReportedByMemberId.ShouldBe(librarian.MemberId);
        report.ReportedByMemberId.ShouldNotBe(LendingTestPrincipals.LibrarianUserId);
        report.ReportReason.ShouldBe("Cracked blade guard");
        report.ObservedCondition.ShouldBe(ToolCondition.Worn);

        var lookup = await _toolInstanceLookupAppService.FindAsync(instance.Id);
        lookup!.CirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
        lookup.Condition.ShouldBe(ToolCondition.Worn);
        lookup.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task An_instance_under_out_of_band_maintenance_cannot_be_reserved()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        await ReportAsLibrarianAsync(instance.Id, ToolCondition.Worn);
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        using (AsMemberWithNoGrants())
        {
            var exception = await Should.ThrowAsync<BusinessException>(() => _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(2)
            }));
            exception.Code.ShouldBe(LendingDomainErrorCodes.InstanceUnavailable);
        }
    }

    [Fact]
    public async Task The_out_of_band_request_appears_in_the_open_queue()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var report = await ReportAsLibrarianAsync(instance.Id, ToolCondition.Worn);

        using (AsLibrarian())
        {
            var open = await _maintenanceRequestAppService.GetOpenListAsync();
            open.Items.ShouldContain(r => r.Id == report.Id && r.Origin == MaintenanceRequestOrigin.OutOfBand);
        }
    }

    [Fact]
    public async Task Closing_an_out_of_band_request_with_zero_cost_returns_the_instance_to_circulation_in_its_observed_condition()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var report = await ReportAsLibrarianAsync(instance.Id, ToolCondition.Worn);

        MaintenanceRequestDto closed;
        using (AsLibrarian())
        {
            closed = await _maintenanceRequestAppService.CloseAsync(report.Id, new CloseMaintenanceRequestDto { Cost = 0m });
        }

        closed.Status.ShouldBe(MaintenanceRequestStatus.Closed);
        closed.Cost.ShouldBe(0m);
        closed.Origin.ShouldBe(MaintenanceRequestOrigin.OutOfBand);

        var lookup = await _toolInstanceLookupAppService.FindAsync(instance.Id);
        lookup!.CirculationState.ShouldBe(ToolInstanceCirculationState.InCirculation);
        lookup.Condition.ShouldBe(ToolCondition.Worn);
        lookup.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task An_equal_observed_condition_sends_the_instance_to_maintenance_without_changing_its_condition()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();

        await ReportAsLibrarianAsync(instance.Id, ToolCondition.Good);

        var lookup = await _toolInstanceLookupAppService.FindAsync(instance.Id);
        lookup!.CirculationState.ShouldBe(ToolInstanceCirculationState.UnderMaintenance);
        lookup.Condition.ShouldBe(ToolCondition.Good);

        var detail = await ToolInstanceAppService.GetAsync(instance.Id);
        detail.History.Count(h => h.PreviousCondition.HasValue && h.PreviousCondition != h.NewCondition).ShouldBe(0);
    }

    [Fact]
    public async Task No_reliability_outcome_is_recorded_for_the_last_borrower_or_the_reporter()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow);

        // A clean loan cycle first, so the instance has a "last borrower".
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
                await _loanAppService.ReturnAsync(loan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good });
            }
        }

        var borrowerBefore = await _memberStandingAppService.GetByIdentityUserIdAsync(LendingTestPrincipals.NoGrantsUserId);
        var reporterBefore = await _memberStandingAppService.GetByIdentityUserIdAsync(LendingTestPrincipals.LibrarianUserId);

        await ReportAsLibrarianAsync(instance.Id, ToolCondition.Damaged);

        var borrowerAfter = await _memberStandingAppService.GetByIdentityUserIdAsync(LendingTestPrincipals.NoGrantsUserId);
        var reporterAfter = await _memberStandingAppService.GetByIdentityUserIdAsync(LendingTestPrincipals.LibrarianUserId);

        borrowerAfter.CurrentRating.ShouldBe(borrowerBefore.CurrentRating);
        reporterAfter.CurrentRating.ShouldBe(reporterBefore.CurrentRating);
    }

    private async Task<MaintenanceRequestDto> ReportAsLibrarianAsync(Guid instanceId, ToolCondition observed)
    {
        using (AsLibrarian())
        {
            return await _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instanceId,
                ObservedCondition = observed,
                Reason = "Found damaged on the shelf"
            });
        }
    }
}

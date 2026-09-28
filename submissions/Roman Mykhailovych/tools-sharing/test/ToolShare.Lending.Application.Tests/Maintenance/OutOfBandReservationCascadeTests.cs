using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using Volo.Abp.EventBus.Local;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 US2 (FR-008, FR-009, SC-002, RES-09): an out-of-band report cancels
/// every not-yet-collected reservation on the instance — including one whose
/// range has already begun — with a reason the holder can see, and generates
/// no notification.
/// </summary>
public class OutOfBandReservationCascadeTests : LendingAuthorizationTestBase
{
    private const string MaintenanceReason = "Instance taken out of circulation for maintenance.";

    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IReservationAppService _reservationAppService;
    private readonly ILocalEventBus _localEventBus;

    public OutOfBandReservationCascadeTests()
    {
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _localEventBus = GetRequiredService<ILocalEventBus>();
    }

    [Fact]
    public async Task Every_uncollected_reservation_is_cancelled_including_one_that_started_today()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        ReservationDto startedToday;
        using (AsMemberWithNoGrants())
        {
            startedToday = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = today,
                EndDate = today.AddDays(2)
            });
        }

        ReservationDto nextWeek;
        using (AsLibrarian())
        {
            nextWeek = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = today.AddDays(7),
                EndDate = today.AddDays(9)
            });
        }

        await ReportAsync(instance.Id);

        var reservations = (await _reservationAppService.GetListForInstanceAsync(instance.Id)).Items;

        foreach (var id in new[] { startedToday.Id, nextWeek.Id })
        {
            var reservation = reservations.Single(r => r.Id == id);
            reservation.Status.ShouldBe(ReservationStatus.Cancelled);
            reservation.CancelledAt.ShouldNotBeNull();
            reservation.CancellationReason.ShouldBe(MaintenanceReason);
        }

        reservations.ShouldNotContain(r => r.Status == ReservationStatus.Active);
    }

    /// <summary>FR-008, US2/AC3: the holder sees the cancellation and its reason in their own reservations.</summary>
    [Fact]
    public async Task The_holder_sees_the_cancellation_and_its_reason_in_their_own_reservations()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var myLendingAppService = GetRequiredService<IMyLendingAppService>();

        ReservationDto reservation;
        using (AsMemberWithNoGrants())
        {
            reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = today.AddDays(3),
                EndDate = today.AddDays(5)
            });
        }

        await ReportAsync(instance.Id);

        using (AsMemberWithNoGrants())
        {
            var mine = (await myLendingAppService.GetMyReservationsAsync()).Single(r => r.Id == reservation.Id);
            mine.Status.ShouldBe(ReservationStatus.Cancelled);
            mine.CancelledAt.ShouldNotBeNull();
            mine.CancellationReason.ShouldBe(MaintenanceReason);
        }
    }

    [Fact]
    public async Task A_reservation_already_checked_out_on_another_instance_is_untouched()
    {
        var (_, _, reported) = await SeedCatalogDataAsync();
        var (_, _, other) = await SeedCatalogDataAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var loanAppService = GetRequiredService<ILoanAppService>();

        ReservationDto otherReservation;
        using (AsMemberWithNoGrants())
        {
            otherReservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = other.Id,
                StartDate = today,
                EndDate = today.AddDays(2)
            });
        }

        using (AsLibrarian())
        {
            await loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = otherReservation.Id });
        }

        await ReportAsync(reported.Id);

        var after = (await _reservationAppService.GetListForInstanceAsync(other.Id)).Items.Single(r => r.Id == otherReservation.Id);
        after.Status.ShouldBe(ReservationStatus.CheckedOut);
    }

    [Fact]
    public async Task No_notification_event_is_raised_by_the_report()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (AsMemberWithNoGrants())
        {
            await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = today.AddDays(3),
                EndDate = today.AddDays(5)
            });
        }

        var raised = new List<LendingNotificationDueEto>();
        using (_localEventBus.Subscribe<LendingNotificationDueEto>(eventData =>
               {
                   raised.Add(eventData);
                   return Task.CompletedTask;
               }))
        {
            await ReportAsync(instance.Id);
        }

        raised.ShouldBeEmpty();
    }

    private async Task ReportAsync(Guid instanceId)
    {
        using (AsLibrarian())
        {
            await _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instanceId,
                ObservedCondition = ToolCondition.Worn,
                Reason = "Cracked blade guard"
            });
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Lending.Reservations;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 FR-014, SC-002, research R4: an out-of-band report racing a reservation
/// for the same instance never leaves an Active reservation on an instance
/// under maintenance. Reservation creation only <i>reads</i> Catalog
/// availability, so without <see cref="IInstanceLock"/> a reservation can
/// commit after the report's cancellation sweep ran and survive it; with the
/// lock the two serialize. (Removing the lock from
/// <c>ReservationAppService.CreateAsync</c> makes this test flaky rather than
/// deterministically red — the race window is narrow — so it is a guard, not
/// a proof.)
/// </summary>
public class ReportVersusReservationConcurrencyTests : LendingAuthorizationTestBase
{
    private const int Iterations = 3;

    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IReservationAppService _reservationAppService;
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;

    public ReportVersusReservationConcurrencyTests()
    {
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _maintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
    }

    [Fact]
    public async Task A_reservation_racing_a_report_is_either_refused_or_cancelled_never_left_active()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var (_, _, instance) = await SeedCatalogDataAsync();
            var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
            var unexpected = new ConcurrentQueue<string>();

            var report = Task.Run(async () =>
            {
                try
                {
                    using (AsLibrarian())
                    {
                        await _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
                        {
                            ToolInstanceId = instance.Id,
                            ObservedCondition = ToolCondition.Good,
                            Reason = "Loose guard"
                        });
                    }
                }
                catch (Exception exception)
                {
                    unexpected.Enqueue($"report: {exception.GetType().Name}: {exception.Message}");
                }
            });

            var reservation = Task.Run(async () =>
            {
                try
                {
                    using (AsMemberWithNoGrants())
                    {
                        await _reservationAppService.CreateAsync(new CreateReservationDto
                        {
                            ToolInstanceId = instance.Id,
                            StartDate = start,
                            EndDate = start.AddDays(2)
                        });
                    }
                }
                catch (BusinessException exception) when (exception.Code == LendingDomainErrorCodes.InstanceUnavailable)
                {
                    // Lost the race cleanly: refused because the instance is already under maintenance.
                }
                catch (Exception exception)
                {
                    unexpected.Enqueue($"reservation: {exception.GetType().Name}: {exception.Message}");
                }
            });

            await Task.WhenAll(report, reservation);

            unexpected.ShouldBeEmpty($"iteration {i}");

            var openRequest = await WithUnitOfWorkAsync(() => _maintenanceRequestRepository.GetOpenForInstanceAsync(instance.Id));
            openRequest.ShouldNotBeNull($"iteration {i}: the report must always succeed");

            var reservations = (await _reservationAppService.GetListForInstanceAsync(instance.Id)).Items;
            reservations.ShouldNotContain(r => r.Status == ReservationStatus.Active, $"iteration {i}");
        }
    }
}

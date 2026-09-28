using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Catalog;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Loans;
using ToolShare.Lending.Reservations;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 FR-014, SC-007: an out-of-band report racing a checkout of the same
/// instance — exactly one wins, and the instance never ends up both on loan
/// and under maintenance. The authority is Catalog's instance row
/// (concurrency stamp + state guard) inside each call's single transaction.
/// </summary>
public class ReportVersusCheckoutConcurrencyTests : LendingAuthorizationTestBase
{
    private const int Iterations = 3;

    private readonly IMaintenanceRequestAppService _maintenanceRequestAppService;
    private readonly IReservationAppService _reservationAppService;
    private readonly ILoanAppService _loanAppService;
    private readonly ILoanRepository _loanRepository;
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;

    public ReportVersusCheckoutConcurrencyTests()
    {
        _maintenanceRequestAppService = GetRequiredService<IMaintenanceRequestAppService>();
        _reservationAppService = GetRequiredService<IReservationAppService>();
        _loanAppService = GetRequiredService<ILoanAppService>();
        _loanRepository = GetRequiredService<ILoanRepository>();
        _maintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
        _toolInstanceLookupAppService = GetRequiredService<IToolInstanceLookupAppService>();
    }

    [Fact]
    public async Task A_report_racing_a_checkout_never_leaves_the_instance_both_on_loan_and_under_maintenance()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var (_, _, instance) = await SeedCatalogDataAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            ReservationDto reservation;
            using (AsMemberWithNoGrants())
            {
                reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
                {
                    ToolInstanceId = instance.Id,
                    StartDate = today,
                    EndDate = today.AddDays(2)
                });
            }

            var failures = new ConcurrentQueue<string>();
            var report = SucceedsAsync(() => _maintenanceRequestAppService.ReportAsync(new ReportMaintenanceDto
            {
                ToolInstanceId = instance.Id,
                ObservedCondition = ToolCondition.Worn,
                Reason = "Cracked blade guard"
            }), "report", failures);
            var checkout = SucceedsAsync(() => _loanAppService.CheckOutAsync(new CheckOutReservationDto { ReservationId = reservation.Id }), "checkout", failures);

            var results = await Task.WhenAll(report, checkout);

            results.Count(succeeded => succeeded).ShouldBe(1,
                $"iteration {i}: exactly one of report/checkout must win; failures: {string.Join(" | ", failures)}");

            var openLoan = await WithUnitOfWorkAsync(() => _loanRepository.GetOpenForInstanceAsync(instance.Id));
            var openRequest = await WithUnitOfWorkAsync(() => _maintenanceRequestRepository.GetOpenForInstanceAsync(instance.Id));
            (openLoan is not null && openRequest is not null).ShouldBeFalse($"iteration {i}: on loan and under maintenance at once");

            var lookup = await _toolInstanceLookupAppService.FindAsync(instance.Id);
            lookup!.CirculationState.ShouldBe(results[0]
                ? ToolInstanceCirculationState.UnderMaintenance
                : ToolInstanceCirculationState.OnLoan);

            await ReleaseMemberCommitmentsAsync(openLoan, instance.Id, reservation.Id);
        }
    }

    /// <summary>
    /// Keeps the one seeded member under their concurrent-loan limit across
    /// iterations: return the loan if checkout won, cancel the reservation if
    /// it is somehow still active after the report won.
    /// </summary>
    private async Task ReleaseMemberCommitmentsAsync(Loan? openLoan, Guid instanceId, Guid reservationId)
    {
        if (openLoan is not null)
        {
            using (AsLibrarian())
            {
                await _loanAppService.ReturnAsync(openLoan.Id, new RecordReturnDto { ReturnedCondition = ToolCondition.Good });
            }

            return;
        }

        var stillActive = (await _reservationAppService.GetListForInstanceAsync(instanceId))
            .Items.Any(r => r.Id == reservationId && r.Status == ReservationStatus.Active);

        if (stillActive)
        {
            using (AsMemberWithNoGrants())
            {
                await _reservationAppService.CancelAsync(reservationId);
            }
        }
    }

    /// <summary>Runs <paramref name="action"/> as the seeded Librarian; the impersonation is scoped to this task's own async flow.</summary>
    private async Task<bool> SucceedsAsync(Func<Task> action, string name, ConcurrentQueue<string> failures)
    {
        try
        {
            using (AsLibrarian())
            {
                await action();
            }

            return true;
        }
        catch (Exception exception)
        {
            failures.Enqueue($"{name}: {exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }
}

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// Verifies plan.md's performance goal (T118): the reservation-eligibility
/// check (availability + standing + overlap pre-check — three live reads
/// across Catalog, Membership, and Lending's own repository) has a warm p95
/// under 150 ms. Each sample creates a reservation against its own freshly
/// seeded instance (reservations can't overlap on one instance), so this
/// measures the live, uncached cost of the full check, not a cache hit. Each
/// sample is cancelled immediately afterward (untimed) so RES-05's
/// concurrent-loan limit — a handful of Active reservations for one member —
/// doesn't start rejecting samples partway through the run.
/// </summary>
public class ReservationEligibilityPerformanceTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;

    public ReservationEligibilityPerformanceTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
    }

    [Fact]
    public async Task Warm_CreateAsync_p95_is_under_150_milliseconds()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        // Warm-up: JIT/connection warm-up, not a cache read (CreateAsync has none).
        var (_, _, warmupInstance) = await SeedCatalogDataAsync();
        var warmupReservation = await _reservationAppService.CreateAsync(new CreateReservationDto
        {
            ToolInstanceId = warmupInstance.Id,
            StartDate = start,
            EndDate = start.AddDays(2)
        });
        await _reservationAppService.CancelAsync(warmupReservation.Id);

        const int sampleCount = 30;
        var samples = new double[sampleCount];
        var stopwatch = new Stopwatch();

        for (var i = 0; i < sampleCount; i++)
        {
            var (_, _, instance) = await SeedCatalogDataAsync();

            stopwatch.Restart();
            var reservation = await _reservationAppService.CreateAsync(new CreateReservationDto
            {
                ToolInstanceId = instance.Id,
                StartDate = start,
                EndDate = start.AddDays(2)
            });
            stopwatch.Stop();

            reservation.Status.ShouldBe(ReservationStatus.Active);
            samples[i] = stopwatch.Elapsed.TotalMilliseconds;

            // Untimed — frees the RES-05 slot for the next sample.
            await _reservationAppService.CancelAsync(reservation.Id);
        }

        var p95 = Percentile(samples, 0.95);
        p95.ShouldBeLessThan(150, $"p95 was {p95:F2} ms across {sampleCount} warm calls");
    }

    private static double Percentile(double[] samples, double percentile)
    {
        var sorted = samples.OrderBy(s => s).ToArray();
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}

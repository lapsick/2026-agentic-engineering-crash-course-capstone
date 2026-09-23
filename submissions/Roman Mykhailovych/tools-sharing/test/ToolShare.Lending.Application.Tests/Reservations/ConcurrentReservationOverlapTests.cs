using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// FR-009: genuinely concurrent reservation attempts for the same instance
/// and overlapping dates — exactly one succeeds, the exclusion constraint
/// (research R3) is the authority. Losing attempts are refused, but not
/// necessarily with a clean <c>BusinessException</c> — Postgres's GiST-backed
/// EXCLUDE constraint can also surface a losing attempt as a deadlock
/// (40P01) under genuine 3-way concurrent contention, which is an accepted
/// outcome of "the other is refused" (FR-009), not a bug — so any exception
/// on a given attempt counts as a loss of the race.
/// </summary>
public class ConcurrentReservationOverlapTests : LendingAuthorizationTestBase
{
    private readonly IReservationAppService _reservationAppService;

    public ConcurrentReservationOverlapTests()
    {
        _reservationAppService = GetRequiredService<IReservationAppService>();
    }

    [Fact]
    public async Task Concurrent_overlapping_attempts_for_one_instance_yield_exactly_one_success()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var end = start.AddDays(3);

        const int concurrentCount = 3;
        var tasks = Enumerable.Range(0, concurrentCount)
            .Select(async _ =>
            {
                try
                {
                    await _reservationAppService.CreateAsync(new CreateReservationDto
                    {
                        ToolInstanceId = instance.Id,
                        StartDate = start,
                        EndDate = end
                    });
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Count(succeeded => succeeded).ShouldBe(1);
    }
}

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// Verifies the plan.md performance goal: "member standing lookup p95 &lt; 50 ms"
/// (T112). Warms the cache with one call, then measures 50 subsequent warm
/// calls to <see cref="IMemberStandingAppService.GetByIdentityUserIdAsync"/> —
/// on the warm path this reads the cached <see cref="MemberStandingProvider"/>
/// snapshot (no member-by-id database round trip) plus one single-row
/// <c>CommunityRules</c> read, so it stays fast even against the real
/// Testcontainers PostgreSQL this test runs against.
/// </summary>
public class MemberStandingLookupPerformanceTests : MembershipAuthorizationTestBase
{
    private readonly IMemberStandingAppService _standingAppService;

    public MemberStandingLookupPerformanceTests()
    {
        _standingAppService = GetRequiredService<IMemberStandingAppService>();
    }

    [Fact]
    public async Task Warm_GetByIdentityUserIdAsync_p95_is_under_50_milliseconds()
    {
        var member = await EnrolAsAdminAsync("Standing Lookup Perf Target");

        // Warm the cache.
        await _standingAppService.GetByIdentityUserIdAsync(member.IdentityUserId);

        const int sampleCount = 50;
        var samples = new double[sampleCount];
        var stopwatch = new Stopwatch();

        for (var i = 0; i < sampleCount; i++)
        {
            stopwatch.Restart();
            var standing = await _standingAppService.GetByIdentityUserIdAsync(member.IdentityUserId);
            stopwatch.Stop();

            standing.IsEnrolled.ShouldBeTrue();
            samples[i] = stopwatch.Elapsed.TotalMilliseconds;
        }

        var p95 = Percentile(samples, 0.95);
        p95.ShouldBeLessThan(50, $"p95 was {p95:F2} ms across {sampleCount} warm calls");
    }

    private static double Percentile(double[] samples, double percentile)
    {
        var sorted = samples.OrderBy(s => s).ToArray();
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}

using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// SC-008 end-to-end, through the app-service layer rather than the domain
/// directly (Phase 2's <c>RatingClampingTests</c> already proved the domain
/// mechanics). Drives clamping via <see cref="IMemberAppService.AdjustRatingAsync"/>
/// (US4 scenario 2): the reliability-reporting app service for the other
/// <see cref="ReliabilityOutcomeType"/> values is US5 (Phase 7) and not
/// available yet, but <see cref="Member.ApplyOutcome"/>'s clamp computation is
/// outcome-type-agnostic, so exercising it through a manual adjustment proves
/// SC-008 at the layer this phase actually publishes.
/// </summary>
public class RatingClampIntegrationTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task A_bonus_applied_at_the_ceiling_clamps_and_records_zero_effective_points()
    {
        var member = await EnrolAsAdminAsync("Clamp Upper Bound Target");
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            // Starts at 100 already — any positive adjustment is immediately clamped.
            var afterBonus = await MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
            {
                Points = 5,
                Reason = "Bonus while already at the ceiling",
                ConcurrencyStamp = member.ConcurrencyStamp
            });

            afterBonus.CurrentRating.ShouldBe(100);

            var history = await MemberAppService.GetStandingHistoryAsync(member.Id);
            var entry = history[^1];

            entry.Kind.ShouldBe(MemberStandingChangeKind.RatingOutcome);
            entry.OutcomeType.ShouldBe(ReliabilityOutcomeType.ManualAdjustment);
            entry.RawPoints.ShouldBe(5);
            entry.EffectivePoints.ShouldBe(0); // clamped: nothing left to add at the ceiling
            entry.ResultingRating.ShouldBe(100);
        }
    }

    [Fact]
    public async Task Repeated_penalties_clamp_at_zero_and_never_go_negative()
    {
        var member = await EnrolAsAdminAsync("Clamp Lower Bound Target");
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            var stamp = member.ConcurrencyStamp;

            // 100 -> 70 -> 40 -> 10 -> clamped to 0 (raw -30 would have gone to -20).
            int[] steps = [-30, -30, -30, -30];
            MemberDetailDto? last = null;
            foreach (var points in steps)
            {
                last = await MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
                {
                    Points = points,
                    Reason = $"Penalty step {points}",
                    ConcurrencyStamp = stamp
                });
                stamp = last.ConcurrencyStamp;
            }

            last!.CurrentRating.ShouldBe(0);

            var history = await MemberAppService.GetStandingHistoryAsync(member.Id);
            var lastEntry = history[^1];

            lastEntry.RawPoints.ShouldBe(-30);
            lastEntry.EffectivePoints.ShouldBe(-10); // 10 -> 0, clamped
            lastEntry.ResultingRating.ShouldBe(0);

            // A further penalty at the floor still records an audit entry with
            // zero effective points rather than being silently dropped.
            var atFloor = await MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
            {
                Points = -20,
                Reason = "Penalty while already at the floor",
                ConcurrencyStamp = stamp
            });

            atFloor.CurrentRating.ShouldBe(0);

            var finalHistory = await MemberAppService.GetStandingHistoryAsync(member.Id);
            var finalEntry = finalHistory[^1];
            finalEntry.RawPoints.ShouldBe(-20);
            finalEntry.EffectivePoints.ShouldBe(0);
            finalEntry.ResultingRating.ShouldBe(0);
        }
    }
}

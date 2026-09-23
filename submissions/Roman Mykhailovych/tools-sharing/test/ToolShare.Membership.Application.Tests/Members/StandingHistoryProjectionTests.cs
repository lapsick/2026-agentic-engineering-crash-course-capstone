using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// HR-06: <c>Member.CurrentRating</c> always equals the <c>ResultingRating</c>
/// of the latest <see cref="MemberStandingChangeKind.RatingOutcome"/> entry.
/// Replays a mixed sequence of rating outcomes (including clamping at both
/// bounds) interleaved with non-rating transitions (deactivate, reactivate,
/// role change, which must never move the projection) and recomputes the
/// rating purely from the stored history, independent of the stored
/// <c>CurrentRating</c> column.
/// </summary>
public class StandingHistoryProjectionTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task CurrentRating_always_equals_the_value_replayed_from_the_rating_outcome_entries()
    {
        var member = await EnrolAsAdminAsync("Projection Target", CommunityRole.Librarian);
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            var stamp = member.ConcurrencyStamp;

            // Mixed sequence: within bounds, clamp high (already can't go
            // above 100 from a fresh member), clamp low, then recover.
            int[] adjustments = [-30, 50, -90, 40];
            MemberDetailDto? afterAdjustments = null;
            foreach (var points in adjustments)
            {
                afterAdjustments = await MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
                {
                    Points = points,
                    Reason = $"Adjustment {points}",
                    ConcurrencyStamp = stamp
                });
                stamp = afterAdjustments.ConcurrencyStamp;
            }

            // Interleave non-rating transitions — they must not perturb the projection.
            var afterDeactivate = await MemberAppService.DeactivateAsync(member.Id, new DeactivateMemberDto
            {
                Reason = "Mid-sequence status change",
                ConcurrencyStamp = stamp
            });
            var afterReactivate = await MemberAppService.ReactivateAsync(member.Id, new ReactivateMemberDto
            {
                ConcurrencyStamp = afterDeactivate.ConcurrencyStamp
            });
            var afterRoleChange = await MemberAppService.ChangeRoleAsync(member.Id, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Administrator,
                ConcurrencyStamp = afterReactivate.ConcurrencyStamp
            });

            var final = await MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
            {
                Points = 15,
                Reason = "Final adjustment",
                ConcurrencyStamp = afterRoleChange.ConcurrencyStamp
            });

            var history = await MemberAppService.GetStandingHistoryAsync(member.Id);

            var replayedRating = history
                .Where(entry => entry.Kind == MemberStandingChangeKind.RatingOutcome)
                .OrderBy(entry => entry.ChangedAt)
                .ThenBy(entry => entry.Id)
                .Aggregate(100, (rating, entry) => rating + entry.EffectivePoints!.Value);

            replayedRating.ShouldBeInRange(0, 100);
            replayedRating.ShouldBe(final.CurrentRating);

            var stored = await MemberAppService.GetAsync(member.Id);
            stored.CurrentRating.ShouldBe(replayedRating);
        }
    }
}

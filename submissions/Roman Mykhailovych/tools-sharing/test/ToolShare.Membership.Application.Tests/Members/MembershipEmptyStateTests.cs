using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// US3 scenario 2: a newly enrolled member reports rating 100 with only the
/// <see cref="MemberStandingChangeKind.Enrolled"/> entry and no error, through
/// the self-service read path (<see cref="IMyMembershipAppService"/>) rather
/// than through the roster-administration one — the score with nothing
/// behind it yet must still render cleanly, not as an empty/broken screen.
/// </summary>
public class MembershipEmptyStateTests : MembershipAuthorizationTestBase
{
    private readonly IMyMembershipAppService _myMembershipAppService;

    public MembershipEmptyStateTests()
    {
        _myMembershipAppService = GetRequiredService<IMyMembershipAppService>();
    }

    [Fact]
    public async Task A_newly_enrolled_member_reports_rating_100_with_only_the_enrolled_entry_and_no_error()
    {
        var member = await EnrolAsAdminAsync("Empty State Member");
        var memberPrincipal = BuildPrincipal(member.IdentityUserId, "empty-state-member");

        using (Impersonate(memberPrincipal))
        {
            var myMembership = await _myMembershipAppService.GetAsync();
            myMembership.CurrentRating.ShouldBe(100);
            myMembership.Status.ShouldBe(MembershipStatus.Active);
            myMembership.Role.ShouldBe(CommunityRole.Member);

            var history = await _myMembershipAppService.GetStandingHistoryAsync();
            history.Count.ShouldBe(1);

            var onlyEntry = history.Single();
            onlyEntry.Kind.ShouldBe(MemberStandingChangeKind.Enrolled);
            onlyEntry.PreviousStatus.ShouldBeNull();
            onlyEntry.PreviousRole.ShouldBeNull();
            onlyEntry.ResultingRating.ShouldBeNull(); // only RatingOutcome rows carry a resulting rating
        }
    }
}

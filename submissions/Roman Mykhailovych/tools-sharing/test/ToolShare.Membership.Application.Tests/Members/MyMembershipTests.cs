using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// FR-022, FR-023, US3 scenario 1: the caller's own status, role, rating,
/// effective concurrent-loan limit and dated standing history are all
/// returned by <see cref="IMyMembershipAppService"/> without passing an id
/// anywhere.
/// </summary>
public class MyMembershipTests : MembershipAuthorizationTestBase
{
    private readonly IMyMembershipAppService _myMembershipAppService;

    public MyMembershipTests()
    {
        _myMembershipAppService = GetRequiredService<IMyMembershipAppService>();
    }

    [Fact]
    public async Task GetAsync_returns_the_callers_own_status_role_rating_and_effective_limit()
    {
        var member = await EnrolAsAdminAsync("Self Service Member");
        var memberPrincipal = BuildPrincipal(member.IdentityUserId, "self-service-member");

        using (Impersonate(memberPrincipal))
        {
            var myMembership = await _myMembershipAppService.GetAsync();

            myMembership.MemberId.ShouldBe(member.Id);
            myMembership.DisplayName.ShouldBe(member.DisplayName);
            myMembership.Status.ShouldBe(MembershipStatus.Active);
            myMembership.Role.ShouldBe(CommunityRole.Member);
            myMembership.CurrentRating.ShouldBe(100);
            myMembership.EffectiveConcurrentLoanLimit.ShouldBe(3); // CommunityRulesConsts.DefaultConcurrentLoanLimit — rating 100 is at/above the default threshold
            // Compared with a tolerance: member.EnrolledAt is the in-memory value from the enrolment
            // response, while myMembership.EnrolledAt round-tripped through PostgreSQL's microsecond
            // timestamp precision, so the two can differ by a fraction of a tick.
            myMembership.EnrolledAt.ShouldBe(member.EnrolledAt, TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task GetStandingHistoryAsync_returns_the_callers_own_dated_history_in_chronological_order()
    {
        var member = await EnrolAsAdminAsync("Self Service History Member", CommunityRole.Librarian);
        var memberPrincipal = BuildPrincipal(member.IdentityUserId, "self-service-history-member");

        using (Impersonate(memberPrincipal))
        {
            var history = await _myMembershipAppService.GetStandingHistoryAsync();

            history.Count.ShouldBe(2);
            history.ElementAt(0).Kind.ShouldBe(MemberStandingChangeKind.Enrolled);
            history.ElementAt(0).PreviousStatus.ShouldBeNull();
            history.ElementAt(0).PreviousRole.ShouldBeNull();
            history.ElementAt(1).Kind.ShouldBe(MemberStandingChangeKind.RoleChanged);
            history.ElementAt(1).PreviousRole.ShouldBe(CommunityRole.Member);
            history.ElementAt(1).NewRole.ShouldBe(CommunityRole.Librarian);

            // Chronological, HR-03.
            history.ElementAt(0).ChangedAt.ShouldBeLessThanOrEqualTo(history.ElementAt(1).ChangedAt);
        }
    }
}

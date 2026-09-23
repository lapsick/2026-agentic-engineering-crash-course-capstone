using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership.Members;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Membership.Authorization;

/// <summary>
/// SC-004: a member calling <see cref="IMemberAppService.GetAsync"/> for
/// another member's id is denied; the same member calling
/// <see cref="IMyMembershipAppService.GetAsync"/> — which takes no id at all —
/// succeeds and returns their own record; a Librarian's
/// <see cref="IMemberAppService.GetAsync"/> for that same other id succeeds.
/// The first and third assertions are already true from US1's
/// authorization (<c>Membership.Members</c> permission); what's new here is
/// proving the self-service surface both succeeds for the caller and offers
/// no parameter through which another member's data could be requested.
/// </summary>
public class SelfOnlyAccessTests : MembershipAuthorizationTestBase
{
    private readonly IMyMembershipAppService _myMembershipAppService;

    public SelfOnlyAccessTests()
    {
        _myMembershipAppService = GetRequiredService<IMyMembershipAppService>();
    }

    [Fact]
    public async Task A_member_cannot_view_another_members_record_through_IMemberAppService_but_can_view_their_own_through_IMyMembershipAppService()
    {
        var self = await EnrolAsAdminAsync("Self Only Access Self Member");
        var other = await EnrolAsAdminAsync("Self Only Access Other Member");

        var selfPrincipal = BuildPrincipal(self.IdentityUserId, "self-only-self-member");

        using (Impersonate(selfPrincipal))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => MemberAppService.GetAsync(other.Id));

            var myMembership = await _myMembershipAppService.GetAsync();
            myMembership.MemberId.ShouldBe(self.Id);
            myMembership.DisplayName.ShouldBe(self.DisplayName);
        }
    }

    [Fact]
    public async Task A_librarian_can_view_another_members_record_through_IMemberAppService()
    {
        var librarian = await EnrolAsAdminAsync("Self Only Access Librarian", CommunityRole.Librarian);
        var other = await EnrolAsAdminAsync("Self Only Access Target Member");

        var librarianPrincipal = BuildPrincipal(librarian.IdentityUserId, "self-only-librarian", "Librarian");

        using (Impersonate(librarianPrincipal))
        {
            var viewedByLibrarian = await MemberAppService.GetAsync(other.Id);
            viewedByLibrarian.Id.ShouldBe(other.Id);
        }
    }
}

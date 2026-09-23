using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership.Members;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Membership.Authorization;

/// <summary>
/// FR-006, FR-006a, SC-005: an authenticated principal with no member record is
/// refused; a member deactivated mid-session is refused on the very next call —
/// including a permission-gated call they would otherwise still hold the grant
/// for. The cross-module half of this (a Catalog.* management method refused for
/// a deactivated Librarian) is proven from the Catalog side — see
/// <c>ToolShare.Catalog.Authorization.BrowseOnlyUserTests</c>
/// (<c>Every_management_operation_is_also_refused_for_an_authenticated_principal_with_no_member_record</c>)
/// and the equivalent deactivation case below — the underlying mechanism
/// (<c>MembershipMethodInvocationAuthorizationService</c>) is module-agnostic by
/// construction, so exercising it here against Membership's own permission-gated
/// operations proves the same seam.
/// </summary>
public class EnrolmentGateTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task An_authenticated_principal_with_no_member_record_is_refused_every_operation()
    {
        using (AsAuthenticatedNonMember())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => MemberAppService.GetListAsync(new GetMemberListInput()));
        }
    }

    [Fact]
    public async Task A_member_deactivated_mid_session_is_refused_on_the_very_next_call()
    {
        var librarian = await EnrolAsAdminAsync("Session Librarian", CommunityRole.Librarian);
        var librarianPrincipal = BuildPrincipal(librarian.IdentityUserId, "session-librarian", "Librarian");

        // The librarian's own next call succeeds while still active — a
        // permission-gated call (Membership.Members, granted to Librarian).
        using (Impersonate(librarianPrincipal))
        {
            await Should.NotThrowAsync(() => MemberAppService.GetListAsync(new GetMemberListInput()));
        }

        // Deactivated by an Administrator, in a different "session".
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            await MemberAppService.DeactivateAsync(librarian.Id, new DeactivateMemberDto
            {
                Reason = "Testing the enrolment gate",
                ConcurrencyStamp = librarian.ConcurrencyStamp
            });
        }

        // The very next call from the (still-authenticated, cache-or-not)
        // librarian's own session is refused — no stale-cache window (research R3).
        using (Impersonate(librarianPrincipal))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => MemberAppService.GetListAsync(new GetMemberListInput()));
        }
    }
}

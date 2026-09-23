using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership.Members;
using Volo.Abp.Identity;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>Research R5: the ABP role set is replaced, never merged, so exactly one role is held; a RoleChanged history entry is appended.</summary>
public class RoleAssignmentTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task Changing_role_replaces_the_abp_role_set_and_appends_one_role_changed_entry()
    {
        var member = await EnrolAsAdminAsync("Role Change Target", CommunityRole.Member);

        var adminPrincipal = await BuildAdminPrincipalAsync();
        MemberDetailDto updated;
        using (Impersonate(adminPrincipal))
        {
            updated = await MemberAppService.ChangeRoleAsync(member.Id, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Librarian,
                ConcurrencyStamp = member.ConcurrencyStamp
            });
        }

        updated.Role.ShouldBe(CommunityRole.Librarian);

        var identityUser = await GetRequiredService<IIdentityUserRepository>().GetAsync(member.IdentityUserId);
        identityUser.Roles.Count.ShouldBe(1);

        var role = await GetRequiredService<IIdentityRoleRepository>().GetAsync(identityUser.Roles.Single().RoleId);
        role.Name.ShouldBe("Librarian");

        var withHistory = await MemberRepository.GetWithHistoryAsync(member.Id);
        withHistory!.StandingHistory.Count.ShouldBe(2);
        withHistory.StandingHistory.ElementAt(1).Kind.ShouldBe(MemberStandingChangeKind.RoleChanged);
        withHistory.StandingHistory.ElementAt(1).PreviousRole.ShouldBe(CommunityRole.Member);
        withHistory.StandingHistory.ElementAt(1).NewRole.ShouldBe(CommunityRole.Librarian);
    }

    [Fact]
    public async Task Changing_role_a_second_time_still_holds_exactly_one_role()
    {
        var member = await EnrolAsAdminAsync("Second Role Change Target", CommunityRole.Librarian);

        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var afterFirst = await MemberAppService.ChangeRoleAsync(member.Id, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Administrator,
                ConcurrencyStamp = member.ConcurrencyStamp
            });

            await MemberAppService.ChangeRoleAsync(member.Id, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Member,
                ConcurrencyStamp = afterFirst.ConcurrencyStamp
            });
        }

        var identityUser = await GetRequiredService<IIdentityUserRepository>().GetAsync(member.IdentityUserId);
        identityUser.Roles.Count.ShouldBe(1);

        var role = await GetRequiredService<IIdentityRoleRepository>().GetAsync(identityUser.Roles.Single().RoleId);
        role.Name.ShouldBe("Member");
    }
}

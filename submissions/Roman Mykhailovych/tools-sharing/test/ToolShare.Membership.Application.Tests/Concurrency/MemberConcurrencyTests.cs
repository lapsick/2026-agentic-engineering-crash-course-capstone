using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership.Members;
using Volo.Abp.Data;
using Xunit;

namespace ToolShare.Membership.Concurrency;

/// <summary>FR-031: a stale ConcurrencyStamp on role change / deactivate throws <see cref="AbpDbConcurrencyException"/>, matching 002's <c>ConcurrentEditTests</c>.</summary>
public class MemberConcurrencyTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task Changing_role_with_a_stale_concurrency_stamp_is_rejected()
    {
        var member = await EnrolAsAdminAsync("Concurrency Role Target");
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            await MemberAppService.ChangeRoleAsync(member.Id, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Librarian,
                ConcurrencyStamp = member.ConcurrencyStamp
            });

            await Should.ThrowAsync<AbpDbConcurrencyException>(() => MemberAppService.ChangeRoleAsync(member.Id, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Administrator,
                ConcurrencyStamp = member.ConcurrencyStamp
            }));
        }
    }

    [Fact]
    public async Task Deactivating_with_a_stale_concurrency_stamp_is_rejected()
    {
        var member = await EnrolAsAdminAsync("Concurrency Deactivate Target");
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            await MemberAppService.ChangeRoleAsync(member.Id, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Librarian,
                ConcurrencyStamp = member.ConcurrencyStamp
            });

            await Should.ThrowAsync<AbpDbConcurrencyException>(() => MemberAppService.DeactivateAsync(member.Id, new DeactivateMemberDto
            {
                Reason = "Stale stamp",
                ConcurrencyStamp = member.ConcurrencyStamp
            }));
        }
    }
}

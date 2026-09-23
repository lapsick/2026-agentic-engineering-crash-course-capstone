using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Identity;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// FR-007, SC-006: deactivating and demoting the only active Administrator both
/// throw <c>Membership:LastAdministrator</c>. The template already seeds two
/// active Administrators (the bootstrap "admin" and the synthetic test-runner
/// principal) — this test enrols a third, then deactivates the other two down
/// to exactly one before exercising the guard on the last one, self-service.
/// </summary>
public class LastAdministratorGuardTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task Deactivating_the_only_active_administrator_is_rejected()
    {
        var soloAdmin = await EnrolAsAdminAsync("Solo Administrator", CommunityRole.Administrator);
        var adminPrincipal = await BuildAdminPrincipalAsync();

        var bootstrapAdminIdentityId = (await GetRequiredService<IIdentityUserRepository>().FindByNormalizedUserNameAsync("ADMIN"))!.Id;
        var bootstrapAdminMember = await MemberRepository.FindByIdentityUserIdAsync(bootstrapAdminIdentityId);
        bootstrapAdminMember.ShouldNotBeNull();

        var testRunnerMember = await MemberRepository.FindByIdentityUserIdAsync(MembershipTestPrincipals.DefaultTestRunnerId);
        testRunnerMember.ShouldNotBeNull();

        using (Impersonate(adminPrincipal))
        {
            // Deactivate the test-runner first, while the bootstrap admin
            // (the currently-impersonated caller) is still active — two other
            // actives remain at this point (bootstrap admin, solo-admin), so
            // allowed. Must happen BEFORE the bootstrap admin deactivates
            // itself below: once that happens, this same impersonated
            // principal would fail the enrolment gate on its very next call.
            var testRunnerDto = await MemberAppService.GetAsync(testRunnerMember.Id);
            await MemberAppService.DeactivateAsync(testRunnerMember.Id, new DeactivateMemberDto
            {
                Reason = "Freeing up a seat for the guard test",
                ConcurrencyStamp = testRunnerDto.ConcurrencyStamp
            });

            // Now deactivate the bootstrap admin itself (self-service) — one
            // other active remains (solo-admin), so allowed.
            var bootstrapAdminDto = await MemberAppService.GetAsync(bootstrapAdminMember.Id);
            await MemberAppService.DeactivateAsync(bootstrapAdminMember.Id, new DeactivateMemberDto
            {
                Reason = "Freeing up a seat for the guard test",
                ConcurrencyStamp = bootstrapAdminDto.ConcurrencyStamp
            });
        }

        // Now impersonate solo-admin — the only remaining active Administrator — and try to deactivate/demote themselves.
        var soloAdminPrincipal = BuildPrincipal(soloAdmin.IdentityUserId, "solo-admin", "Administrator");
        using (Impersonate(soloAdminPrincipal))
        {
            var freshSoloAdmin = await MemberAppService.GetAsync(soloAdmin.Id);

            var deactivateException = await Should.ThrowAsync<BusinessException>(() => MemberAppService.DeactivateAsync(soloAdmin.Id, new DeactivateMemberDto
            {
                Reason = "Should be rejected",
                ConcurrencyStamp = freshSoloAdmin.ConcurrencyStamp
            }));
            deactivateException.Code.ShouldBe(MembershipDomainErrorCodes.LastAdministrator);

            var changeRoleException = await Should.ThrowAsync<BusinessException>(() => MemberAppService.ChangeRoleAsync(soloAdmin.Id, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Member,
                ConcurrencyStamp = freshSoloAdmin.ConcurrencyStamp
            }));
            changeRoleException.Code.ShouldBe(MembershipDomainErrorCodes.LastAdministrator);
        }
    }
}

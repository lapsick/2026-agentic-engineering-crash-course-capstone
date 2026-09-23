using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership.Members;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Membership.Authorization;

/// <summary>SC-003: every roster operation denied for an active member without Membership grants and for an anonymous principal; allowed for an Administrator.</summary>
public class RosterAuthorizationTests : MembershipAuthorizationTestBase
{
    private List<(string Name, Func<Task> Action)> BuildRosterOperations(Guid targetMemberId, string concurrencyStamp)
    {
        return new List<(string, Func<Task>)>
        {
            ("GetList", () => MemberAppService.GetListAsync(new GetMemberListInput())),
            ("Get", () => MemberAppService.GetAsync(targetMemberId)),
            ("Enrol", () => MemberAppService.EnrolAsync(new EnrolMemberDto
            {
                DisplayName = "Denied Enrolment",
                Email = $"{Guid.NewGuid():N}@example.com",
                InitialPassword = "Passw0rd!123"
            })),
            ("ChangeRole", () => MemberAppService.ChangeRoleAsync(targetMemberId, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Librarian,
                ConcurrencyStamp = concurrencyStamp
            })),
            ("Deactivate", () => MemberAppService.DeactivateAsync(targetMemberId, new DeactivateMemberDto
            {
                Reason = "Should be denied",
                ConcurrencyStamp = concurrencyStamp
            })),
            ("Reactivate", () => MemberAppService.ReactivateAsync(targetMemberId, new ReactivateMemberDto
            {
                ConcurrencyStamp = concurrencyStamp
            }))
        };
    }

    [Fact]
    public async Task Every_roster_operation_is_denied_for_an_active_member_without_membership_grants()
    {
        var target = await EnrolAsAdminAsync("Roster Target For No Grants Test");

        using (AsMemberWithNoGrants())
        {
            foreach (var (name, action) in BuildRosterOperations(target.Id, target.ConcurrencyStamp))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(action, $"{name} should have been denied for a member with no Membership grants");
            }
        }
    }

    [Fact]
    public async Task Every_roster_operation_is_denied_for_an_anonymous_principal()
    {
        var target = await EnrolAsAdminAsync("Roster Target For Anonymous Test");

        using (AsAnonymous())
        {
            foreach (var (name, action) in BuildRosterOperations(target.Id, target.ConcurrencyStamp))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(action, $"{name} should have been denied for an anonymous principal");
            }
        }
    }

    [Fact]
    public async Task Every_roster_operation_succeeds_for_an_administrator()
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            var target = await MemberAppService.EnrolAsync(new EnrolMemberDto
            {
                DisplayName = "Roster Target For Admin Test",
                Email = $"{Guid.NewGuid():N}@example.com",
                InitialPassword = "Passw0rd!123"
            });

            await Should.NotThrowAsync(() => MemberAppService.GetListAsync(new GetMemberListInput()));
            await Should.NotThrowAsync(() => MemberAppService.GetAsync(target.Id));

            var afterRoleChange = await MemberAppService.ChangeRoleAsync(target.Id, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Librarian,
                ConcurrencyStamp = target.ConcurrencyStamp
            });

            var afterDeactivate = await MemberAppService.DeactivateAsync(target.Id, new DeactivateMemberDto
            {
                Reason = "Testing admin access",
                ConcurrencyStamp = afterRoleChange.ConcurrencyStamp
            });

            await Should.NotThrowAsync(() => MemberAppService.ReactivateAsync(target.Id, new ReactivateMemberDto
            {
                ConcurrencyStamp = afterDeactivate.ConcurrencyStamp
            }));
        }
    }
}

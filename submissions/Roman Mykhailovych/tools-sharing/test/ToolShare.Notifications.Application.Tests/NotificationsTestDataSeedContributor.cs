using System;
using System.Threading.Tasks;
using ToolShare.Membership;
using ToolShare.Membership.Members;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace ToolShare.Notifications;

/// <summary>
/// Seeds enrolled, Active member records for Notifications' own fixed
/// synthetic test principals (<see cref="NotificationsTestPrincipals"/>),
/// mirroring <c>LendingTestDataSeedContributor</c>. Idempotent (checks by
/// identity user id before inserting).
/// </summary>
public class NotificationsTestDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IMemberRepository _memberRepository;
    private readonly MemberManager _memberManager;

    public NotificationsTestDataSeedContributor(IMemberRepository memberRepository, MemberManager memberManager)
    {
        _memberRepository = memberRepository;
        _memberManager = memberManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await EnsureMemberAsync(NotificationsTestPrincipals.DefaultTestRunnerId, "Default Test Runner", CommunityRole.Administrator, $"{NotificationsTestPrincipals.DefaultTestRunnerId:N}@notifications-tests.local");
        await EnsureMemberAsync(NotificationsTestPrincipals.NoGrantsUserId, "No Grants Test User", CommunityRole.Member, $"{NotificationsTestPrincipals.NoGrantsUserId:N}@notifications-tests.local");
        await EnsureMemberAsync(NotificationsTestPrincipals.OtherMemberId, "Other Test Member", CommunityRole.Member, $"{NotificationsTestPrincipals.OtherMemberId:N}@notifications-tests.local");
        // FR-009's "no email on file" case (US2): every member enrolled
        // through Membership's domain layer always has a non-blank email
        // (Member.SetEmail enforces it, even bypassing EnrolMemberDto's
        // [Required] validation) — so this member is seeded with a normal
        // email like any other, and EmailMaskingMemberStandingAppService
        // (registered only in the test module) strips it specifically for
        // this identity when Notifications asks Membership for standing,
        // isolating the "no email on file" condition without violating a
        // real invariant Membership itself guarantees.
        await EnsureMemberAsync(NotificationsTestPrincipals.NoEmailUserId, "No Email Test User", CommunityRole.Member, $"{NotificationsTestPrincipals.NoEmailUserId:N}@notifications-tests.local");
    }

    private async Task EnsureMemberAsync(Guid identityUserId, string displayName, CommunityRole role, string email)
    {
        if (await _memberRepository.FindByIdentityUserIdAsync(identityUserId) is not null)
        {
            return;
        }

        var member = await _memberManager.CreateAsync(
            identityUserId,
            displayName,
            email,
            DateTime.UtcNow,
            enrolledByUserId: null);

        if (role != CommunityRole.Member)
        {
            await _memberManager.ChangeRoleAsync(member, role, DateTime.UtcNow, byUserId: null);
        }

        await _memberRepository.InsertAsync(member, autoSave: true);
    }
}

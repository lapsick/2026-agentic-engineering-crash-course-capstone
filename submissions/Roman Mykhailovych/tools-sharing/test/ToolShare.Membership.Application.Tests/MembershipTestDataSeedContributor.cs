using System;
using System.Threading.Tasks;
using ToolShare.Membership.Members;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace ToolShare.Membership;

/// <summary>
/// Seeds enrolled, Active member records for Membership's own fixed synthetic
/// test principals (<see cref="MembershipTestPrincipals"/>) so their suites
/// survive the enrolment gate they are themselves testing — filled in for US1
/// (T044+), as flagged at the equivalent Phase 2 checkpoint. Idempotent
/// (checks by identity user id before inserting), mirroring
/// <c>MembershipDataSeedContributor</c>.
/// </summary>
public class MembershipTestDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IMemberRepository _memberRepository;
    private readonly MemberManager _memberManager;

    public MembershipTestDataSeedContributor(IMemberRepository memberRepository, MemberManager memberManager)
    {
        _memberRepository = memberRepository;
        _memberManager = memberManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await EnsureMemberAsync(MembershipTestPrincipals.DefaultTestRunnerId, "Default Test Runner", CommunityRole.Administrator);
        await EnsureMemberAsync(MembershipTestPrincipals.NoGrantsMemberId, "No Grants Test Member", CommunityRole.Member);
    }

    private async Task EnsureMemberAsync(Guid identityUserId, string displayName, CommunityRole role)
    {
        if (await _memberRepository.FindByIdentityUserIdAsync(identityUserId) is not null)
        {
            return;
        }

        var member = await _memberManager.CreateAsync(
            identityUserId,
            displayName,
            $"{identityUserId:N}@membership-tests.local",
            DateTime.UtcNow,
            enrolledByUserId: null);

        if (role != CommunityRole.Member)
        {
            await _memberManager.ChangeRoleAsync(member, role, DateTime.UtcNow, byUserId: null);
        }

        await _memberRepository.InsertAsync(member, autoSave: true);
    }
}

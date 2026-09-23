using System;
using System.Threading.Tasks;
using ToolShare.Membership;
using ToolShare.Membership.Members;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace ToolShare.Lending;

/// <summary>
/// Seeds enrolled, Active member records for Lending's own fixed synthetic
/// test principals (<see cref="LendingTestPrincipals"/>) so their suites
/// survive the enrolment gate, mirroring <c>CatalogTestDataSeedContributor</c>/
/// <c>MembershipTestDataSeedContributor</c>. Idempotent (checks by identity
/// user id before inserting).
/// </summary>
public class LendingTestDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IMemberRepository _memberRepository;
    private readonly MemberManager _memberManager;

    public LendingTestDataSeedContributor(IMemberRepository memberRepository, MemberManager memberManager)
    {
        _memberRepository = memberRepository;
        _memberManager = memberManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await EnsureMemberAsync(LendingTestPrincipals.DefaultTestRunnerId, "Default Test Runner", CommunityRole.Administrator);
        await EnsureMemberAsync(LendingTestPrincipals.NoGrantsUserId, "No Grants Test User", CommunityRole.Member);
        await EnsureMemberAsync(LendingTestPrincipals.LibrarianUserId, "Librarian Test User", CommunityRole.Librarian);
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
            $"{identityUserId:N}@lending-tests.local",
            DateTime.UtcNow,
            enrolledByUserId: null);

        if (role != CommunityRole.Member)
        {
            await _memberManager.ChangeRoleAsync(member, role, DateTime.UtcNow, byUserId: null);
        }

        await _memberRepository.InsertAsync(member, autoSave: true);
    }
}

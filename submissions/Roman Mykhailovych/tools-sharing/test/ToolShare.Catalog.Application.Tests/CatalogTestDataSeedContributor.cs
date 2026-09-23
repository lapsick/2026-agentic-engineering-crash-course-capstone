using System;
using System.Threading.Tasks;
using ToolShare.Membership;
using ToolShare.Membership.Members;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace ToolShare.Catalog;

/// <summary>
/// Extension point for test-only seed data. Since 003 (research R10, T063):
/// seeds enrolled, Active member records for Catalog's fixed synthetic test
/// principals (<see cref="CatalogTestPrincipals"/>) so their suites survive the
/// enrolment gate — <c>MembershipMethodInvocationAuthorizationService</c>
/// refuses every application-service call, Catalog's included, unless the
/// caller resolves to an enrolled Active member. Idempotent (checks by identity
/// user id before inserting), mirroring <c>MembershipDataSeedContributor</c>.
/// </summary>
public class CatalogTestDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IMemberRepository _memberRepository;
    private readonly MemberManager _memberManager;

    public CatalogTestDataSeedContributor(IMemberRepository memberRepository, MemberManager memberManager)
    {
        _memberRepository = memberRepository;
        _memberManager = memberManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await EnsureMemberAsync(CatalogTestPrincipals.DefaultTestRunnerId, "Default Test Runner", CommunityRole.Administrator);
        await EnsureMemberAsync(CatalogTestPrincipals.NoGrantsUserId, "No Grants Test User", CommunityRole.Member);
        await EnsureMemberAsync(CatalogTestPrincipals.LibrarianUserId, "Librarian Test User", CommunityRole.Librarian);
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
            $"{identityUserId:N}@catalog-tests.local",
            DateTime.UtcNow,
            enrolledByUserId: null);

        if (role != CommunityRole.Member)
        {
            await _memberManager.ChangeRoleAsync(member, role, DateTime.UtcNow, byUserId: null);
        }

        await _memberRepository.InsertAsync(member, autoSave: true);
    }
}

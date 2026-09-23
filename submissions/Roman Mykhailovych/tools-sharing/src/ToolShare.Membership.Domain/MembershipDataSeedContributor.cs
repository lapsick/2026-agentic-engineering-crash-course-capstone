using System;
using System.Threading.Tasks;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace ToolShare.Membership;

/// <summary>
/// Idempotent seeding for a fresh install (mirrors 002's FR-016 requirement).
/// Seeds the <see cref="CommunityRules.CommunityRules"/> singleton (CRR-02) and,
/// if the host supplies the bootstrap administrator's identity user id via
/// <see cref="DataSeedContext"/> properties (wired up in Phase 3/US1, alongside
/// role seeding — see <c>LibrarianRoleDataSeedContributor</c>), the bootstrap
/// administrator's <see cref="Member"/> record (FR-010). Membership.Domain must
/// not depend on the Identity module (research R2), so it cannot resolve the
/// admin identity user id itself; it only reacts to properties the host chooses
/// to supply.
/// </summary>
public class MembershipDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    /// <summary>
    /// <see cref="DataSeedContext"/> property name the host may set (to a
    /// boxed <see cref="Guid"/>) to the bootstrap administrator's identity
    /// user id so this contributor can create their <see cref="Member"/>
    /// record. Absent by default — no bootstrap member is created until the
    /// host wires this up.
    /// </summary>
    public const string AdminIdentityUserIdPropertyName = "Membership.AdminIdentityUserId";

    /// <summary><see cref="DataSeedContext"/> property name for the bootstrap administrator's display name.</summary>
    public const string AdminDisplayNamePropertyName = "Membership.AdminDisplayName";

    /// <summary><see cref="DataSeedContext"/> property name for the bootstrap administrator's email.</summary>
    public const string AdminEmailPropertyName = "Membership.AdminEmail";

    private readonly ICommunityRulesRepository _communityRulesRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly MemberManager _memberManager;

    public MembershipDataSeedContributor(
        ICommunityRulesRepository communityRulesRepository,
        IMemberRepository memberRepository,
        MemberManager memberManager)
    {
        _communityRulesRepository = communityRulesRepository;
        _memberRepository = memberRepository;
        _memberManager = memberManager;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await SeedCommunityRulesAsync();
        await SeedBootstrapAdministratorAsync(context);
    }

    /// <summary>Inserts the rules row only if absent (CRR-02); never updates an existing one.</summary>
    private async Task SeedCommunityRulesAsync()
    {
        if (await _communityRulesRepository.GetCurrentAsync() is not null)
        {
            return;
        }

        var rules = new CommunityRules.CommunityRules(Membership.CommunityRulesConsts.SingletonId);
        await _communityRulesRepository.InsertAsync(rules, autoSave: true);
    }

    private async Task SeedBootstrapAdministratorAsync(DataSeedContext? context)
    {
        if (context is null || context[AdminIdentityUserIdPropertyName] is not Guid adminIdentityUserId)
        {
            return;
        }

        if (await _memberRepository.FindByIdentityUserIdAsync(adminIdentityUserId) is not null)
        {
            return;
        }

        var displayName = context[AdminDisplayNamePropertyName] as string ?? "Administrator";
        var email = context[AdminEmailPropertyName] as string ?? "admin@toolshare.local";

        var member = await _memberManager.CreateAsync(adminIdentityUserId, displayName, email, DateTime.UtcNow, enrolledByUserId: null);
        member.ChangeRole(CommunityRole.Administrator, DateTime.UtcNow, byUserId: null);

        await _memberRepository.InsertAsync(member, autoSave: true);
    }
}

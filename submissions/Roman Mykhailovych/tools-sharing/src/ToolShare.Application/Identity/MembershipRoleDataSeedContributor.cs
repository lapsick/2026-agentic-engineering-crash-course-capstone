using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ToolShare.Membership.Permissions;
using ToolShare.Notifications.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;

namespace ToolShare.Identity;

/// <summary>
/// Creates the "Member" and "Administrator" ABP roles and grants the cumulative
/// permissions from contracts/membership-permissions.md#roles-and-grants (research
/// R5): ABP grants permissions per role and models no hierarchy, so
/// "Administrator ⊇ Librarian ⊇ Member" has to be realised by granting the union
/// at seed time. Lives in the host, alongside <see cref="LibrarianRoleDataSeedContributor"/>,
/// for the same reason that one does — granting permissions requires the Identity
/// module, which Membership itself must not depend on. Idempotent: roles are
/// created only if absent and grants go through <see cref="IPermissionDataSeeder"/>,
/// itself idempotent.
/// </summary>
public class MembershipRoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string MemberRoleName = "Member";

    public const string AdministratorRoleName = "Administrator";

    /// <summary>Everything a Librarian has, plus every Membership.* administration permission (research R5).</summary>
    private static readonly string[] AdministratorPermissions = LibrarianRoleDataSeedContributor.CatalogManagementPermissions
        .Concat(LibrarianRoleDataSeedContributor.LibrarianMembershipPermissions)
        .Concat(LibrarianRoleDataSeedContributor.LibrarianLendingPermissions)
        .Concat(new[]
        {
            MembershipPermissions.Members.Default,
            MembershipPermissions.Members.Enrol,
            MembershipPermissions.Members.ChangeRole,
            MembershipPermissions.Members.Deactivate,
            MembershipPermissions.Members.AdjustRating,
            MembershipPermissions.Rules.Default,
            MembershipPermissions.Rules.Edit,
            NotificationsPermissions.Audit
        })
        .Distinct()
        .ToArray();

    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IdentityRoleManager _roleManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IPermissionDataSeeder _permissionDataSeeder;

    public MembershipRoleDataSeedContributor(
        IIdentityRoleRepository roleRepository,
        IdentityRoleManager roleManager,
        IGuidGenerator guidGenerator,
        IPermissionDataSeeder permissionDataSeeder)
    {
        _roleRepository = roleRepository;
        _roleManager = roleManager;
        _guidGenerator = guidGenerator;
        _permissionDataSeeder = permissionDataSeeder;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await EnsureRoleAsync(MemberRoleName, context?.TenantId);
        // "Member" is granted nothing — browsing is gated by the enrolment gate,
        // not a permission (the same reasoning 002 used to keep browsing
        // permission-free).

        await EnsureRoleAsync(AdministratorRoleName, context?.TenantId);
        await _permissionDataSeeder.SeedAsync(
            RolePermissionValueProvider.ProviderName,
            AdministratorRoleName,
            AdministratorPermissions,
            context?.TenantId);
    }

    private async Task EnsureRoleAsync(string roleName, System.Guid? tenantId)
    {
        var role = await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
        if (role != null)
        {
            return;
        }

        role = new IdentityRole(_guidGenerator.Create(), roleName, tenantId)
        {
            IsStatic = false,
            IsPublic = true
        };
        (await _roleManager.CreateAsync(role)).CheckErrors();
    }
}

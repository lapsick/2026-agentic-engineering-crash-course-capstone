using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ToolShare.Catalog.Permissions;
using ToolShare.Lending.Permissions;
using ToolShare.Membership.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;

namespace ToolShare.Identity;

/// <summary>
/// Grants every Catalog.* management permission — plus, since 003, the roster
/// "view" permission and reliability reporting (research R5: 003's Librarian is
/// the one processing a return in 004+) — to a "Librarian" role, creating the
/// role only if it does not already exist. Lives in the host, not in Catalog or
/// Membership, because granting permissions requires the Identity module — a
/// business module must not take that dependency (see contracts/catalog-permissions.md,
/// contracts/membership-permissions.md).
/// </summary>
public class LibrarianRoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string RoleName = "Librarian";

    /// <summary>Every Catalog.* management permission. Reused by <see cref="MembershipRoleDataSeedContributor"/> when composing the Administrator role's cumulative grants (research R5).</summary>
    public static readonly string[] CatalogManagementPermissions =
    {
        CatalogPermissions.Categories.Create,
        CatalogPermissions.Categories.Edit,
        CatalogPermissions.Categories.Delete,
        CatalogPermissions.Tools.Create,
        CatalogPermissions.Tools.Edit,
        CatalogPermissions.Tools.Delete,
        CatalogPermissions.ToolInstances.Create,
        CatalogPermissions.ToolInstances.Edit,
        CatalogPermissions.ToolInstances.ChangeCondition,
        CatalogPermissions.ToolInstances.Retire,
        CatalogPermissions.ToolInstances.ManagePhotos,
        // 004-lending: the Librarian processing a checkout/return/maintenance
        // closure is the one reporting these facts into Catalog (research R2).
        CatalogPermissions.ToolInstances.ReportLendingState
    };

    /// <summary>The Membership grants a Librarian holds (research R5): roster view + reliability reporting.</summary>
    public static readonly string[] LibrarianMembershipPermissions =
    {
        MembershipPermissions.Members.Default,
        MembershipPermissions.Reliability.Report
    };

    /// <summary>004-lending: checking out/returning a loan (US2) and closing a maintenance request (US3).</summary>
    public static readonly string[] LibrarianLendingPermissions =
    {
        LendingPermissions.Loans.Default,
        LendingPermissions.Loans.Checkout,
        LendingPermissions.Loans.Return,
        LendingPermissions.Maintenance.Close,
        // 006-librarian-reports: the three librarian reports (FR-012). Adding it
        // here also grants Administrator, since MembershipRoleDataSeedContributor
        // composes its cumulative grants from this array.
        LendingPermissions.Reports.Default
    };

    private static readonly string[] LibrarianPermissions = CatalogManagementPermissions
        .Concat(LibrarianMembershipPermissions)
        .Concat(LibrarianLendingPermissions)
        .ToArray();

    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IdentityRoleManager _roleManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IPermissionDataSeeder _permissionDataSeeder;

    public LibrarianRoleDataSeedContributor(
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
        var role = await _roleRepository.FindByNormalizedNameAsync(RoleName.ToUpperInvariant());
        if (role == null)
        {
            role = new IdentityRole(_guidGenerator.Create(), RoleName, context?.TenantId)
            {
                IsStatic = false,
                IsPublic = true
            };
            (await _roleManager.CreateAsync(role)).CheckErrors();
        }

        await _permissionDataSeeder.SeedAsync(
            RolePermissionValueProvider.ProviderName,
            RoleName,
            LibrarianPermissions,
            context?.TenantId);
    }
}

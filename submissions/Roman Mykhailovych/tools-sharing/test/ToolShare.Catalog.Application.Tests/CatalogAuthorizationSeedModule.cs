using ToolShare.EntityFrameworkCore;
using ToolShare.Membership;
using ToolShare.Membership.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace ToolShare.Catalog;

/// <summary>
/// A throwaway ABP application used exactly once per test assembly run, purely to
/// seed the TEMPLATE database (see <see cref="CatalogApplicationTestFixture"/>)
/// with the same admin/permission bootstrap that <c>ToolShare.DbMigrator</c>
/// produces in production (Phase 5/US3, US4). Every per-test database is then
/// cloned from this already-seeded template, so individual tests never re-run
/// the seed pipeline themselves.
///
/// Unlike <see cref="CatalogApplicationTestModule"/>, this module deliberately
/// does NOT override individual DbContexts' connection strings: the full seed
/// pipeline (<c>IdentityDataSeeder</c>, <c>PermissionDataSeedContributor</c>,
/// <c>LibrarianRoleDataSeedContributor</c>, and the template's own
/// <c>OpenIddictDataSeedContributor</c>) touches several host DbContexts
/// (Identity, PermissionManagement, SettingManagement, OpenIddict, ...), all of
/// which resolve the standard "Default" connection string name. The caller
/// supplies that connection string via configuration instead — see
/// <see cref="CatalogApplicationTestFixture"/> — exactly like every host
/// DbContext already resolves it in production.
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(ToolShareApplicationModule),
    typeof(ToolShareEntityFrameworkCoreModule),
    // 003 ripple (research R10, T063): needed to seed member records — via
    // CatalogTestDataSeedContributor — for Catalog's own test principals into
    // the template database.
    typeof(MembershipApplicationModule),
    typeof(MembershipEntityFrameworkCoreModule)
    )]
public class CatalogAuthorizationSeedModule : AbpModule
{
}

using ToolShare.Catalog;
using ToolShare.EntityFrameworkCore;
using ToolShare.Membership;
using ToolShare.Membership.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace ToolShare.Lending;

/// <summary>
/// A throwaway ABP application used exactly once per test assembly run, purely
/// to seed the TEMPLATE database (see <see cref="LendingApplicationTestFixture"/>)
/// with the same admin/permission bootstrap <c>ToolShare.DbMigrator</c> produces
/// in production, plus Lending's own test-principal member records
/// (<see cref="LendingTestDataSeedContributor"/>) — mirrors
/// <c>CatalogAuthorizationSeedModule</c>/<c>MembershipAuthorizationSeedModule</c>,
/// extended with Catalog's own module since Lending's suite is the first to
/// need all three business modules composed at once (research R9).
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(ToolShareApplicationModule),
    typeof(ToolShareEntityFrameworkCoreModule),
    typeof(CatalogApplicationModule),
    typeof(CatalogEntityFrameworkCoreModule),
    typeof(MembershipApplicationModule),
    typeof(MembershipEntityFrameworkCoreModule)
    )]
public class LendingAuthorizationSeedModule : AbpModule
{
}

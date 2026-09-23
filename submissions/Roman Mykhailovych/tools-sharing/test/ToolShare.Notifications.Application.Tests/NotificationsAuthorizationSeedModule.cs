using ToolShare.Catalog;
using ToolShare.EntityFrameworkCore;
using ToolShare.Membership;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace ToolShare.Notifications;

/// <summary>
/// A throwaway ABP application used exactly once per test assembly run, purely
/// to seed the TEMPLATE database (see <see cref="NotificationsApplicationTestFixture"/>)
/// with the same admin/permission bootstrap <c>ToolShare.DbMigrator</c> produces
/// in production, plus Notifications' own test-principal member records —
/// mirrors <c>LendingAuthorizationSeedModule</c>.
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
public class NotificationsAuthorizationSeedModule : AbpModule
{
}

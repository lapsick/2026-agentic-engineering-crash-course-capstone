using ToolShare.Catalog;
using ToolShare.EntityFrameworkCore;
using ToolShare.Lending;
using ToolShare.Membership;
using ToolShare.Notifications;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace ToolShare.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(ToolShareEntityFrameworkCoreModule),
    typeof(ToolShareApplicationModule),
    typeof(CatalogApplicationModule),
    typeof(CatalogEntityFrameworkCoreModule),
    typeof(MembershipApplicationModule),
    typeof(MembershipEntityFrameworkCoreModule),
    typeof(LendingApplicationModule),
    typeof(LendingEntityFrameworkCoreModule),
    typeof(NotificationsApplicationModule),
    typeof(NotificationsEntityFrameworkCoreModule)
    )]
public class ToolShareDbMigratorModule : AbpModule
{
}

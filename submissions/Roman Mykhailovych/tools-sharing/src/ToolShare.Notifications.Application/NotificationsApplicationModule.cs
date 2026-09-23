using ToolShare.Catalog;
using ToolShare.Membership;
using Volo.Abp.Application;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.Modularity;

namespace ToolShare.Notifications;

[DependsOn(
    typeof(NotificationsDomainModule),
    typeof(NotificationsApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpBackgroundJobsModule),
    typeof(AbpEmailingModule),
    typeof(CatalogApplicationContractsModule),
    typeof(MembershipApplicationContractsModule)
    )]
public class NotificationsApplicationModule : AbpModule
{
}

using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace ToolShare.Notifications;

[DependsOn(
    typeof(NotificationsDomainSharedModule),
    typeof(AbpDddDomainModule)
    )]
public class NotificationsDomainModule : AbpModule
{
}

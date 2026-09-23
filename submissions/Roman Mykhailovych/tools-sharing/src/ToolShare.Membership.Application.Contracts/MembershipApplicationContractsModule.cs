using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace ToolShare.Membership;

[DependsOn(
    typeof(MembershipDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class MembershipApplicationContractsModule : AbpModule
{
}

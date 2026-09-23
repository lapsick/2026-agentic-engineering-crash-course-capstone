using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace ToolShare.Lending;

[DependsOn(
    typeof(LendingDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class LendingApplicationContractsModule : AbpModule
{
}

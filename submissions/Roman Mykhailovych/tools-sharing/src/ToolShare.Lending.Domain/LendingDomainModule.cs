using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace ToolShare.Lending;

[DependsOn(
    typeof(LendingDomainSharedModule),
    typeof(AbpDddDomainModule)
    )]
public class LendingDomainModule : AbpModule
{
}

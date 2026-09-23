using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace ToolShare.Membership;

[DependsOn(
    typeof(MembershipDomainSharedModule),
    typeof(AbpDddDomainModule)
    )]
public class MembershipDomainModule : AbpModule
{
}

using Volo.Abp.Modularity;

namespace ToolShare;

[DependsOn(
    typeof(ToolShareDomainModule),
    typeof(ToolShareTestBaseModule)
)]
public class ToolShareDomainTestModule : AbpModule
{

}

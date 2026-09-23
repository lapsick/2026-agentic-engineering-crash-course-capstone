using Volo.Abp.Modularity;

namespace ToolShare;

[DependsOn(
    typeof(ToolShareApplicationModule),
    typeof(ToolShareDomainTestModule)
)]
public class ToolShareApplicationTestModule : AbpModule
{

}

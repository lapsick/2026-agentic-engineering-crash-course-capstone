using Volo.Abp.Modularity;

namespace ToolShare;

public abstract class ToolShareApplicationTestBase<TStartupModule> : ToolShareTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}

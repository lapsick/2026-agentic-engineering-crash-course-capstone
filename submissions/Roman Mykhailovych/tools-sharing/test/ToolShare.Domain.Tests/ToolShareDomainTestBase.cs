using Volo.Abp.Modularity;

namespace ToolShare;

/* Inherit from this class for your domain layer tests. */
public abstract class ToolShareDomainTestBase<TStartupModule> : ToolShareTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}

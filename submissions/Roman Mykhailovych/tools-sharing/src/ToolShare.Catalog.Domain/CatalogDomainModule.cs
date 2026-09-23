using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace ToolShare.Catalog;

[DependsOn(
    typeof(CatalogDomainSharedModule),
    typeof(AbpDddDomainModule)
    )]
public class CatalogDomainModule : AbpModule
{
}

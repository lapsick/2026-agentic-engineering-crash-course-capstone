using Volo.Abp.Application;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace ToolShare.Catalog;

[DependsOn(
    typeof(CatalogDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class CatalogApplicationContractsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Per contracts/catalog-app-services.md: default MaxResultCount 10, hard cap 100.
        LimitedResultRequestDto.MaxMaxResultCount = 100;
    }
}

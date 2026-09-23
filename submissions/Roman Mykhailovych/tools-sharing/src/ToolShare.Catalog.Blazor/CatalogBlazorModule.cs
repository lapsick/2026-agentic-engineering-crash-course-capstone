using ToolShare.Catalog.Blazor.Menus;
using Volo.Abp.AspNetCore.Components.Web;
using Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor.Routing;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;

namespace ToolShare.Catalog.Blazor;

[DependsOn(
    typeof(CatalogApplicationContractsModule),
    typeof(AbpAspNetCoreComponentsWebModule)
    )]
public class CatalogBlazorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpRouterOptions>(options =>
        {
            options.AdditionalAssemblies.Add(typeof(CatalogBlazorModule).Assembly);
        });

        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new CatalogMenuContributor());
        });
    }
}

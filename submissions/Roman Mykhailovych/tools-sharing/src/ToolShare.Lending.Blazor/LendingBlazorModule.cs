using ToolShare.Lending.Blazor.Menus;
using Volo.Abp.AspNetCore.Components.Web;
using Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor.Routing;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;

namespace ToolShare.Lending.Blazor;

[DependsOn(
    typeof(LendingApplicationContractsModule),
    typeof(AbpAspNetCoreComponentsWebModule)
    )]
public class LendingBlazorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpRouterOptions>(options =>
        {
            options.AdditionalAssemblies.Add(typeof(LendingBlazorModule).Assembly);
        });

        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new LendingMenuContributor());
        });
    }
}

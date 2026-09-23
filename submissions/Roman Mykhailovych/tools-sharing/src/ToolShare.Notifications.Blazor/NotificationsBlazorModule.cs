using ToolShare.Notifications.Blazor.Components;
using ToolShare.Notifications.Blazor.Menus;
using Volo.Abp.AspNetCore.Components.Web;
using Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor.Routing;
using Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor.Toolbars;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;

namespace ToolShare.Notifications.Blazor;

[DependsOn(
    typeof(NotificationsApplicationContractsModule),
    typeof(AbpAspNetCoreComponentsWebModule)
    )]
public class NotificationsBlazorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpRouterOptions>(options =>
        {
            options.AdditionalAssemblies.Add(typeof(NotificationsBlazorModule).Assembly);
        });

        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new NotificationsMenuContributor());
        });

        // US2/FR-003: the live unread bell in the main toolbar.
        Configure<AbpToolbarOptions>(options =>
        {
            options.Contributors.Add(new NotificationBellToolbarContributor());
        });
    }
}

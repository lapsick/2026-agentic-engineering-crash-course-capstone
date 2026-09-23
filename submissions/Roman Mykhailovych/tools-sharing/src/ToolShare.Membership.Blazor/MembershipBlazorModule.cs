using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Membership.Blazor.Menus;
using ToolShare.Membership.Blazor.Security;
using Volo.Abp.AspNetCore.Components.Web;
using Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor.Routing;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;

namespace ToolShare.Membership.Blazor;

[DependsOn(
    typeof(MembershipApplicationContractsModule),
    typeof(AbpAspNetCoreComponentsWebModule)
    )]
public class MembershipBlazorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpRouterOptions>(options =>
        {
            options.AdditionalAssemblies.Add(typeof(MembershipBlazorModule).Assembly);
        });

        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new MembershipMenuContributor());
        });

        // The UI-affordance half of the enrolment gate (research R3, T062) —
        // the authoritative half is MembershipMethodInvocationAuthorizationService,
        // registered by MembershipApplicationModule. ToolShareBlazorModule wires
        // this requirement into the router's fallback policy alongside the
        // existing authentication requirement.
        context.Services.AddHttpContextAccessor();
        context.Services.AddTransient<IAuthorizationHandler, MembershipActiveMemberRequirementHandler>();
    }
}

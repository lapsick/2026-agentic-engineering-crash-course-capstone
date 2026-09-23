using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ToolShare.Localization;
using ToolShare.MultiTenancy;
using Volo.Abp.Account.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity.Blazor.MudBlazor;
using Volo.Abp.SettingManagement.Blazor.MudBlazor.Menus;
using Volo.Abp.TenantManagement.Blazor.MudBlazor.Navigation;
using Volo.Abp.UI.Navigation;

namespace ToolShare.Blazor.Menus;

public class ToolShareMenuContributor : IMenuContributor
{
    private readonly IConfiguration _configuration;

    public ToolShareMenuContributor(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
        else if (context.Menu.Name == StandardMenus.User)
        {
            await ConfigureUserMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<ToolShareResource>();

        context.Menu.Items.Insert(
            0,
            new ApplicationMenuItem(
                ToolShareMenus.Home,
                l["Menu:Home"],
                "/",
                icon: "fas fa-home"
            )
        );

        var administration = context.Menu.GetAdministration();

        if (MultiTenancyConsts.IsEnabled)
        {
            administration.SetSubItemOrder(TenantManagementMenuNames.GroupName, 1);
        }
        else
        {
            administration.TryRemoveMenuItem(TenantManagementMenuNames.GroupName);
        }

        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);
        administration.SetSubItemOrder(SettingManagementMenus.GroupName, 3);

        return Task.CompletedTask;
    }

    private Task ConfigureUserMenuAsync(MenuConfigurationContext context)
    {
        // Server-only host (no WASM) — always add the account link, unlike the
        // template's WASM/Server-hybrid guard (`OperatingSystem.IsBrowser()`),
        // which would permanently hide this item under InteractiveServer-only rendering.
        var authServerUrl = _configuration["AuthServer:Authority"] ?? "";
        var accountStringLocalizer = context.GetLocalizer<AccountResource>();

        context.Menu.AddItem(new ApplicationMenuItem(
                "Account.Manage",
                accountStringLocalizer["MyAccount"],
                $"{authServerUrl.EnsureEndsWith('/')}Account/Manage",
                icon: "fa fa-cog",
                order: 1000,
                target: "_blank")
            .RequireAuthenticated());

        return Task.CompletedTask;
    }
}

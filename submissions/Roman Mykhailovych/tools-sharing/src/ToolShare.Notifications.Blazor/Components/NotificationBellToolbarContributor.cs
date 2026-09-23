using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor.Toolbars;

namespace ToolShare.Notifications.Blazor.Components;

/// <summary>
/// US2/FR-003: mounts the live <see cref="NotificationBell"/> into the application's main toolbar so the
/// unread indicator is visible — and updates live — from every page, replacing the navigation-time
/// menu-label count (007-realtime-notifications, research R1).
/// </summary>
public class NotificationBellToolbarContributor : IToolbarContributor
{
    public Task ConfigureToolbarAsync(IToolbarConfigurationContext context)
    {
        if (context.Toolbar.Name == StandardToolbars.Main)
        {
            context.Toolbar.Items.Add(new ToolbarItem(typeof(NotificationBell)));
        }

        return Task.CompletedTask;
    }
}

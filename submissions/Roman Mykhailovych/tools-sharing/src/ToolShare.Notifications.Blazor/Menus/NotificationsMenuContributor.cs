using System.Threading.Tasks;
using ToolShare.Notifications.Localization;
using Volo.Abp.UI.Navigation;

namespace ToolShare.Notifications.Blazor.Menus;

public class NotificationsMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            ConfigureMainMenu(context);
        }

        return Task.CompletedTask;
    }

    private static void ConfigureMainMenu(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<NotificationsResource>();

        // The unread count now lives on the live toolbar bell (007-realtime-notifications, US2) — the
        // one authoritative, real-time home for it — so the menu item is a plain link. Previously the
        // count was appended to this label and only recomputed on navigation.
        // FR-012: a member's own inbox carries no permission requirement beyond the enrolment gate,
        // mirroring Membership's MyMembership and Lending's MyReservations menu entries.
        context.Menu.AddItem(new ApplicationMenuItem(
            NotificationsMenuNames.MyNotifications,
            l["Menu:Notifications.MyNotifications"].Value,
            "/notifications/my",
            icon: "fas fa-bell"
        ));
    }
}

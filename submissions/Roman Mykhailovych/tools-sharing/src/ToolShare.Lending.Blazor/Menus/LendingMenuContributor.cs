using System.Threading.Tasks;
using ToolShare.Lending.Localization;
using ToolShare.Lending.Permissions;
using Volo.Abp.UI.Navigation;

namespace ToolShare.Lending.Blazor.Menus;

public class LendingMenuContributor : IMenuContributor
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
        var l = context.GetLocalizer<LendingResource>();

        var lendingMenu = new ApplicationMenuItem(
            LendingMenuNames.GroupName,
            l["Menu:Lending"],
            icon: "fas fa-calendar-check"
        );

        // FR-029: creating/cancelling one's own reservation carries no
        // permission requirement beyond the enrolment gate, so — mirroring
        // Membership's MyMembership entry — this item carries no
        // requiredPermissionName either.
        lendingMenu.AddItem(new ApplicationMenuItem(
            LendingMenuNames.MyReservations,
            l["Menu:Lending.MyReservations"],
            "/lending/my-reservations"
        ));

        lendingMenu.AddItem(new ApplicationMenuItem(
            LendingMenuNames.Loans,
            l["Menu:Lending.Loans"],
            "/lending/loans",
            requiredPermissionName: LendingPermissions.Loans.Default
        ));

        lendingMenu.AddItem(new ApplicationMenuItem(
            LendingMenuNames.MaintenanceRequests,
            l["Menu:Lending.MaintenanceRequests"],
            "/lending/maintenance",
            requiredPermissionName: LendingPermissions.Loans.Default
        ));

        // 006-librarian-reports: hidden rather than shown-then-refused, so a
        // plain Member never sees the entry (FR-012).
        lendingMenu.AddItem(new ApplicationMenuItem(
            LendingMenuNames.Reports,
            l["Menu:Lending.Reports"],
            "/lending/reports",
            requiredPermissionName: LendingPermissions.Reports.Default
        ));

        context.Menu.AddItem(lendingMenu);
    }
}

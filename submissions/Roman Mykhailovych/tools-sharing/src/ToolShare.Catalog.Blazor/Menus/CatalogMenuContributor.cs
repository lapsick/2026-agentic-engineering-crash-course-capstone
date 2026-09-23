using System.Threading.Tasks;
using ToolShare.Catalog.Localization;
using ToolShare.Catalog.Permissions;
using Volo.Abp.UI.Navigation;

namespace ToolShare.Catalog.Blazor.Menus;

public class CatalogMenuContributor : IMenuContributor
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
        var l = context.GetLocalizer<CatalogResource>();

        var catalogMenu = new ApplicationMenuItem(
            CatalogMenuNames.GroupName,
            l["Menu:Catalog"],
            icon: "fas fa-boxes"
        );

        // Browsing tools is authentication-gated, not permission-gated (US2) —
        // any signed-in user may see this entry.
        catalogMenu.AddItem(new ApplicationMenuItem(
            CatalogMenuNames.Tools,
            l["Menu:Catalog.Tools"],
            "/catalog"
        ));

        // Categories is a management surface: its CRUD page is only useful to
        // someone who can actually create/edit/delete a category.
        catalogMenu.AddItem(new ApplicationMenuItem(
            CatalogMenuNames.Categories,
            l["Menu:Catalog.Categories"],
            "/catalog/categories",
            requiredPermissionName: CatalogPermissions.Categories.Create
        ));

        context.Menu.AddItem(catalogMenu);
    }
}

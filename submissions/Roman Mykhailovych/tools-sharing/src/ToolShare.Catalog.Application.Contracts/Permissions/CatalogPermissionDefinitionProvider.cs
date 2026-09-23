using ToolShare.Catalog.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace ToolShare.Catalog.Permissions;

public class CatalogPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var catalogGroup = context.AddGroup(CatalogPermissions.GroupName, L("Permission:Catalog"));

        var categoriesPermission = catalogGroup.AddPermission(CatalogPermissions.Categories.Default, L("Permission:Catalog.Categories"));
        categoriesPermission.AddChild(CatalogPermissions.Categories.Create, L("Permission:Catalog.Categories.Create"));
        categoriesPermission.AddChild(CatalogPermissions.Categories.Edit, L("Permission:Catalog.Categories.Edit"));
        categoriesPermission.AddChild(CatalogPermissions.Categories.Delete, L("Permission:Catalog.Categories.Delete"));

        var toolsPermission = catalogGroup.AddPermission(CatalogPermissions.Tools.Default, L("Permission:Catalog.Tools"));
        toolsPermission.AddChild(CatalogPermissions.Tools.Create, L("Permission:Catalog.Tools.Create"));
        toolsPermission.AddChild(CatalogPermissions.Tools.Edit, L("Permission:Catalog.Tools.Edit"));
        toolsPermission.AddChild(CatalogPermissions.Tools.Delete, L("Permission:Catalog.Tools.Delete"));

        var instancesPermission = catalogGroup.AddPermission(CatalogPermissions.ToolInstances.Default, L("Permission:Catalog.ToolInstances"));
        instancesPermission.AddChild(CatalogPermissions.ToolInstances.Create, L("Permission:Catalog.ToolInstances.Create"));
        instancesPermission.AddChild(CatalogPermissions.ToolInstances.Edit, L("Permission:Catalog.ToolInstances.Edit"));
        instancesPermission.AddChild(CatalogPermissions.ToolInstances.ChangeCondition, L("Permission:Catalog.ToolInstances.ChangeCondition"));
        instancesPermission.AddChild(CatalogPermissions.ToolInstances.Retire, L("Permission:Catalog.ToolInstances.Retire"));
        instancesPermission.AddChild(CatalogPermissions.ToolInstances.ManagePhotos, L("Permission:Catalog.ToolInstances.ManagePhotos"));
        instancesPermission.AddChild(CatalogPermissions.ToolInstances.ReportLendingState, L("Permission:Catalog.ToolInstances.ReportLendingState"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<CatalogResource>(name);
    }
}

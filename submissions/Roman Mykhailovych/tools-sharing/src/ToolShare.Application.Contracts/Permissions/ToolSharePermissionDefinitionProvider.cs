using ToolShare.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace ToolShare.Permissions;

public class ToolSharePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(ToolSharePermissions.GroupName);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(ToolSharePermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<ToolShareResource>(name);
    }
}

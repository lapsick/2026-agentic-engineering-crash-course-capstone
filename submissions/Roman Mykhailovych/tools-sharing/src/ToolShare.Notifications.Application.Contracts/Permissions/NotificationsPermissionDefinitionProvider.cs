using ToolShare.Notifications.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace ToolShare.Notifications.Permissions;

public class NotificationsPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var notificationsGroup = context.AddGroup(NotificationsPermissions.GroupName, L("Permission:Notifications"));

        notificationsGroup.AddPermission(NotificationsPermissions.Audit, L("Permission:Notifications.Audit"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<NotificationsResource>(name);
    }
}

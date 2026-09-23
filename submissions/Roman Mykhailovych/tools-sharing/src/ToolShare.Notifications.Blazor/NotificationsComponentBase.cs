using ToolShare.Notifications.Localization;
using Volo.Abp.AspNetCore.Components;

namespace ToolShare.Notifications.Blazor;

/// <summary>Base for every Notifications Blazor page/component — mirrors <c>LendingComponentBase</c>.</summary>
public abstract class NotificationsComponentBase : AbpComponentBase
{
    protected NotificationsComponentBase()
    {
        LocalizationResource = typeof(NotificationsResource);
    }
}

using ToolShare.Localization;
using Volo.Abp.AspNetCore.Components;

namespace ToolShare.Blazor;

public abstract class ToolShareComponentBase : AbpComponentBase
{
    protected ToolShareComponentBase()
    {
        LocalizationResource = typeof(ToolShareResource);
    }
}

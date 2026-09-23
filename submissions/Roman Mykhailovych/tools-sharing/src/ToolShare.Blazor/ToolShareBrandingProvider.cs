using Microsoft.Extensions.Localization;
using ToolShare.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace ToolShare.Blazor;

[Dependency(ReplaceServices = true)]
public class ToolShareBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<ToolShareResource> _localizer;

    public ToolShareBrandingProvider(IStringLocalizer<ToolShareResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}

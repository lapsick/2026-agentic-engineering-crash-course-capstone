using Volo.Abp.Settings;

namespace ToolShare.Settings;

public class ToolShareSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(ToolShareSettings.MySetting1));
    }
}

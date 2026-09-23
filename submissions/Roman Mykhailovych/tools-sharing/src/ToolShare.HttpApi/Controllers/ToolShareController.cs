using ToolShare.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace ToolShare.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class ToolShareController : AbpControllerBase
{
    protected ToolShareController()
    {
        LocalizationResource = typeof(ToolShareResource);
    }
}

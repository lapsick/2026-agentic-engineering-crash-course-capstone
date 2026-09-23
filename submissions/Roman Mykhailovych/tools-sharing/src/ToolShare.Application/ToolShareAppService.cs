using System;
using System.Collections.Generic;
using System.Text;
using ToolShare.Localization;
using Volo.Abp.Application.Services;

namespace ToolShare;

/* Inherit your application services from this class.
 */
public abstract class ToolShareAppService : ApplicationService
{
    protected ToolShareAppService()
    {
        LocalizationResource = typeof(ToolShareResource);
    }
}

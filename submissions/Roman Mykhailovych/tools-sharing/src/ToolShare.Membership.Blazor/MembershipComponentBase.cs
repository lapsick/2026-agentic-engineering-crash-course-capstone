using System;
using System.Linq;
using System.Threading.Tasks;
using ToolShare.Membership.Localization;
using Volo.Abp;
using Volo.Abp.AspNetCore.Components;
using Volo.Abp.Data;

namespace ToolShare.Membership.Blazor;

/// <summary>Base for every Membership Blazor page/component — mirrors <c>CatalogComponentBase</c>.</summary>
public abstract class MembershipComponentBase : AbpComponentBase
{
    protected MembershipComponentBase()
    {
        LocalizationResource = typeof(MembershipResource);
    }

    protected virtual async Task<bool> TryRunAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (AbpDbConcurrencyException)
        {
            await Message.Warn(L["Membership:ConcurrencyConflict"]);
            return false;
        }
        catch (BusinessException businessException) when (!string.IsNullOrEmpty(businessException.Code) && businessException.Data.Count > 0)
        {
            var args = businessException.Data.Values.Cast<object>().ToArray();
            await Message.Error(L[businessException.Code, args]);
            return false;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            return false;
        }
    }
}

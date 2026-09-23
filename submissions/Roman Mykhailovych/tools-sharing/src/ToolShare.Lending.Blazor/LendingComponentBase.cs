using System;
using System.Linq;
using System.Threading.Tasks;
using ToolShare.Lending.Localization;
using Volo.Abp;
using Volo.Abp.AspNetCore.Components;
using Volo.Abp.Data;

namespace ToolShare.Lending.Blazor;

/// <summary>Base for every Lending Blazor page/component — mirrors <c>CatalogComponentBase</c>/<c>MembershipComponentBase</c>.</summary>
public abstract class LendingComponentBase : AbpComponentBase
{
    protected LendingComponentBase()
    {
        LocalizationResource = typeof(LendingResource);
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
            await Message.Warn(L["Lending:ConcurrencyConflict"]);
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

using System;
using System.Linq;
using System.Threading.Tasks;
using ToolShare.Catalog.Localization;
using Volo.Abp;
using Volo.Abp.AspNetCore.Components;
using Volo.Abp.Data;

namespace ToolShare.Catalog.Blazor;

/// <summary>
/// Base for every Catalog Blazor page/component. A stale
/// <see cref="AbpDbConcurrencyException"/> (FR-010) needs a distinct, more actionable
/// message, and a <see cref="BusinessException"/> with <c>.WithData(...)</c> arguments
/// (e.g. <c>Catalog:PhotoTooLarge</c>, <c>Catalog:CategoryNameAlreadyExists</c>) needs its
/// placeholder(s) resolved explicitly here — found via live UI testing that the base
/// <see cref="AbpComponentBase.HandleErrorAsync"/>/<c>IUserExceptionInformer</c> pipeline
/// resolves the exception <c>Code</c> to its localized template but does **not** substitute
/// <c>exception.Data.Values</c> into it, so end users saw the raw, unsubstituted
/// <c>"…already uses the name '{0}'."</c> instead of the actual value. Everything else still
/// goes through the standard pipeline.
/// </summary>
public abstract class CatalogComponentBase : AbpComponentBase
{
    protected CatalogComponentBase()
    {
        LocalizationResource = typeof(CatalogResource);
    }

    /// <summary>
    /// Runs <paramref name="action"/>, surfacing a concurrency conflict as a
    /// reload-and-retry message, a parameterized <see cref="BusinessException"/> with its
    /// placeholder(s) resolved, and everything else through the standard
    /// localized-exception pipeline. Returns whether it completed without error.
    /// </summary>
    protected virtual async Task<bool> TryRunAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (AbpDbConcurrencyException)
        {
            await Message.Warn(L["Catalog:ConcurrencyConflict"]);
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

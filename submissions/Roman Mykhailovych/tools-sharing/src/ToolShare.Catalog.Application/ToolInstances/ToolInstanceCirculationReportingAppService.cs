using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Catalog.Permissions;
using Volo.Abp.Application.Services;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// The inbound counterpart to <see cref="IToolInstanceLookupAppService"/>: a
/// downstream module (Lending, 004) reports lending-driven facts about an
/// instance it does not own. See specs/004-lending/contracts/catalog-extension.md.
/// </summary>
[Authorize(CatalogPermissions.ToolInstances.ReportLendingState)]
public class ToolInstanceCirculationReportingAppService : ApplicationService, IToolInstanceCirculationReportingAppService
{
    private readonly IToolInstanceRepository _toolInstanceRepository;

    public ToolInstanceCirculationReportingAppService(IToolInstanceRepository toolInstanceRepository)
    {
        _toolInstanceRepository = toolInstanceRepository;
    }

    public virtual async Task MarkOnLoanAsync(Guid toolInstanceId)
    {
        var instance = await _toolInstanceRepository.GetAsync(toolInstanceId);
        instance.MarkOnLoan(Clock.Now, CurrentUser.Id);
        await _toolInstanceRepository.UpdateAsync(instance, autoSave: true);
    }

    public virtual async Task MarkReturnedAsync(Guid toolInstanceId, ToolCondition returnedCondition)
    {
        var instance = await _toolInstanceRepository.GetAsync(toolInstanceId);
        instance.Return(returnedCondition, Clock.Now, CurrentUser.Id);
        await _toolInstanceRepository.UpdateAsync(instance, autoSave: true);
    }

    public virtual async Task MarkReturnedForMaintenanceAsync(Guid toolInstanceId, ToolCondition returnedCondition)
    {
        var instance = await _toolInstanceRepository.GetAsync(toolInstanceId);
        instance.ReturnForMaintenance(returnedCondition, Clock.Now, CurrentUser.Id);
        await _toolInstanceRepository.UpdateAsync(instance, autoSave: true);
    }

    public virtual async Task MarkSentToMaintenanceAsync(Guid toolInstanceId, ToolCondition observedCondition, string reason)
    {
        var instance = await _toolInstanceRepository.GetAsync(toolInstanceId);
        instance.SendToMaintenance(observedCondition, reason, Clock.Now, CurrentUser.Id);
        await _toolInstanceRepository.UpdateAsync(instance, autoSave: true);
    }

    public virtual async Task MarkMaintenanceClosedAsync(Guid toolInstanceId)
    {
        var instance = await _toolInstanceRepository.GetAsync(toolInstanceId);
        instance.CloseMaintenance(Clock.Now, CurrentUser.Id);
        await _toolInstanceRepository.UpdateAsync(instance, autoSave: true);
    }
}

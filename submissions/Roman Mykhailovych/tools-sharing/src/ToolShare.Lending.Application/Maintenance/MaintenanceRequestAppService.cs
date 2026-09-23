using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Permissions;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.CommunityRules;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// Closing an open maintenance request (US3): restores the instance in
/// Catalog and, if a waitlist exists for it, offers the earliest waiting
/// member in the same operation.
/// </summary>
[Authorize(LendingPermissions.Loans.Default)]
public class MaintenanceRequestAppService : ApplicationService, IMaintenanceRequestAppService
{
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;
    private readonly WaitlistManager _waitlistManager;
    private readonly IToolInstanceCirculationReportingAppService _toolInstanceCirculationReportingAppService;
    private readonly ICommunityRulesLookupAppService _communityRulesLookupAppService;

    public MaintenanceRequestAppService(
        IMaintenanceRequestRepository maintenanceRequestRepository,
        WaitlistManager waitlistManager,
        IToolInstanceCirculationReportingAppService toolInstanceCirculationReportingAppService,
        ICommunityRulesLookupAppService communityRulesLookupAppService)
    {
        _maintenanceRequestRepository = maintenanceRequestRepository;
        _waitlistManager = waitlistManager;
        _toolInstanceCirculationReportingAppService = toolInstanceCirculationReportingAppService;
        _communityRulesLookupAppService = communityRulesLookupAppService;
    }

    [Authorize(LendingPermissions.Maintenance.Close)]
    public virtual async Task<MaintenanceRequestDto> CloseAsync(Guid id, CloseMaintenanceRequestDto input)
    {
        var request = await _maintenanceRequestRepository.GetAsync(id);
        var closedAt = Clock.Now;

        request.Close(closedAt, input.Cost);
        await _maintenanceRequestRepository.UpdateAsync(request, autoSave: true);

        await _toolInstanceCirculationReportingAppService.MarkMaintenanceClosedAsync(request.ToolInstanceId);

        var rules = await _communityRulesLookupAppService.GetAsync();
        await _waitlistManager.OfferNextAsync(request.ToolInstanceId, closedAt, rules.WaitlistOfferWindowHours);

        return MapToDto(request);
    }

    public virtual async Task<PagedResultDto<MaintenanceRequestDto>> GetOpenListAsync()
    {
        var requests = await _maintenanceRequestRepository.GetAllOpenAsync();
        return new PagedResultDto<MaintenanceRequestDto>(requests.Count, requests.Select(MapToDto).ToList());
    }

    private static MaintenanceRequestDto MapToDto(MaintenanceRequest request) => new()
    {
        Id = request.Id,
        ToolInstanceId = request.ToolInstanceId,
        TriggeringLoanId = request.TriggeringLoanId,
        Status = request.Status,
        OpenedAt = request.OpenedAt,
        ClosedAt = request.ClosedAt,
        Cost = request.Cost
    };
}

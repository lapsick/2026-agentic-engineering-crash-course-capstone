using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Permissions;
using ToolShare.Lending.Reservations;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Users;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// Closing an open maintenance request (US3): restores the instance in
/// Catalog and, if a waitlist exists for it, offers the earliest waiting
/// member in the same operation. 008 adds the out-of-band report — the second
/// way a request opens (contracts/lending-maintenance.md).
/// </summary>
[Authorize(LendingPermissions.Loans.Default)]
public class MaintenanceRequestAppService : ApplicationService, IMaintenanceRequestAppService
{
    /// <summary>008 RES-09: shown to the holder in their own reservations (FR-008).</summary>
    public const string OutOfBandCancellationReason = "Instance taken out of circulation for maintenance.";

    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;
    private readonly MaintenanceManager _maintenanceManager;
    private readonly IInstanceLock _instanceLock;
    private readonly ReservationManager _reservationManager;
    private readonly WaitlistManager _waitlistManager;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;
    private readonly IToolInstanceCirculationReportingAppService _toolInstanceCirculationReportingAppService;
    private readonly ICommunityRulesLookupAppService _communityRulesLookupAppService;
    private readonly IMemberStandingAppService _memberStandingAppService;

    public MaintenanceRequestAppService(
        IMaintenanceRequestRepository maintenanceRequestRepository,
        MaintenanceManager maintenanceManager,
        IInstanceLock instanceLock,
        ReservationManager reservationManager,
        WaitlistManager waitlistManager,
        IToolInstanceLookupAppService toolInstanceLookupAppService,
        IToolInstanceCirculationReportingAppService toolInstanceCirculationReportingAppService,
        ICommunityRulesLookupAppService communityRulesLookupAppService,
        IMemberStandingAppService memberStandingAppService)
    {
        _maintenanceRequestRepository = maintenanceRequestRepository;
        _maintenanceManager = maintenanceManager;
        _instanceLock = instanceLock;
        _reservationManager = reservationManager;
        _waitlistManager = waitlistManager;
        _toolInstanceLookupAppService = toolInstanceLookupAppService;
        _toolInstanceCirculationReportingAppService = toolInstanceCirculationReportingAppService;
        _communityRulesLookupAppService = communityRulesLookupAppService;
        _memberStandingAppService = memberStandingAppService;
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

    /// <summary>
    /// 008 US1–US3. One transaction: the instance lock is taken before any
    /// Catalog read (research R4), every check runs before the first write
    /// (FR-005), and a failure anywhere — including Catalog's own guard —
    /// rolls everything back. Reports no reliability outcome (MAINT-08) and
    /// raises no notification (FR-009).
    /// </summary>
    [Authorize(LendingPermissions.Maintenance.Report)]
    public virtual async Task<MaintenanceRequestDto> ReportAsync(ReportMaintenanceDto input)
    {
        await _instanceLock.LockInstanceAsync(input.ToolInstanceId);

        var instance = await _toolInstanceLookupAppService.FindAsync(input.ToolInstanceId)
            ?? throw new BusinessException(LendingDomainErrorCodes.InstanceUnavailable);
        var reporter = await _memberStandingAppService.GetByIdentityUserIdAsync(CurrentUser.GetId());
        var at = Clock.Now;

        var request = await _maintenanceManager.ReportOutOfBandAsync(
            input.ToolInstanceId,
            instance.CirculationState,
            instance.Condition,
            input.ObservedCondition,
            input.Reason,
            reporter.MemberId!.Value,
            at);

        await _maintenanceRequestRepository.InsertAsync(request, autoSave: true);
        await _toolInstanceCirculationReportingAppService.MarkSentToMaintenanceAsync(input.ToolInstanceId, input.ObservedCondition, request.ReportReason!);

        await _reservationManager.CancelAllUncollectedForMaintenanceAsync(input.ToolInstanceId, at, OutOfBandCancellationReason);
        await _waitlistManager.WithdrawOutstandingOfferAsync(input.ToolInstanceId, at);

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
        Origin = request.Origin,
        TriggeringLoanId = request.TriggeringLoanId,
        ReportedByMemberId = request.ReportedByMemberId,
        ReportReason = request.ReportReason,
        ObservedCondition = request.ObservedCondition,
        Status = request.Status,
        OpenedAt = request.OpenedAt,
        ClosedAt = request.ClosedAt,
        Cost = request.Cost
    };
}

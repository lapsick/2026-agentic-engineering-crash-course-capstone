using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Maintenance;
using ToolShare.Lending.Permissions;
using ToolShare.Lending.Reservations;
using ToolShare.Membership;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ToolShare.Lending.Loans;

/// <summary>Checkout, return, and the roster view (US2, US4).</summary>
[Authorize(LendingPermissions.Loans.Default)]
public class LoanAppService : ApplicationService, ILoanAppService
{
    private readonly ILoanRepository _loanRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;
    private readonly LoanManager _loanManager;
    private readonly ReservationManager _reservationManager;
    private readonly WaitlistManager _waitlistManager;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;
    private readonly IToolInstanceCirculationReportingAppService _toolInstanceCirculationReportingAppService;
    private readonly ICommunityRulesLookupAppService _communityRulesLookupAppService;
    private readonly IReliabilityReportingAppService _reliabilityReportingAppService;

    public LoanAppService(
        ILoanRepository loanRepository,
        IReservationRepository reservationRepository,
        IMaintenanceRequestRepository maintenanceRequestRepository,
        LoanManager loanManager,
        ReservationManager reservationManager,
        WaitlistManager waitlistManager,
        IToolInstanceLookupAppService toolInstanceLookupAppService,
        IToolInstanceCirculationReportingAppService toolInstanceCirculationReportingAppService,
        ICommunityRulesLookupAppService communityRulesLookupAppService,
        IReliabilityReportingAppService reliabilityReportingAppService)
    {
        _loanRepository = loanRepository;
        _reservationRepository = reservationRepository;
        _maintenanceRequestRepository = maintenanceRequestRepository;
        _loanManager = loanManager;
        _reservationManager = reservationManager;
        _waitlistManager = waitlistManager;
        _toolInstanceLookupAppService = toolInstanceLookupAppService;
        _toolInstanceCirculationReportingAppService = toolInstanceCirculationReportingAppService;
        _communityRulesLookupAppService = communityRulesLookupAppService;
        _reliabilityReportingAppService = reliabilityReportingAppService;
    }

    [Authorize(LendingPermissions.Loans.Checkout)]
    public virtual async Task<LoanDto> CheckOutAsync(CheckOutReservationDto input)
    {
        var reservation = await _reservationRepository.GetAsync(input.ReservationId);

        var instance = await _toolInstanceLookupAppService.FindAsync(reservation.ToolInstanceId)
            ?? throw new BusinessException(LendingDomainErrorCodes.InstanceUnavailable);
        var isAvailable = await _toolInstanceLookupAppService.IsAvailableAsync(reservation.ToolInstanceId);

        var loan = await _loanManager.CheckOutAsync(reservation, isAvailable, Clock.Now, instance.Condition);

        await _reservationRepository.UpdateAsync(reservation, autoSave: true);
        await _loanRepository.InsertAsync(loan, autoSave: true);

        await _toolInstanceCirculationReportingAppService.MarkOnLoanAsync(reservation.ToolInstanceId);

        return MapToDto(loan);
    }

    /// <summary>
    /// LOAN-03/LOAN-04/LOAN-05. Structured so a retried call after a
    /// mid-close crash (membership-consumption.md) can resume where it left
    /// off: the Catalog-facing half (<see cref="LoanManager.ReturnAsync"/>,
    /// opening a maintenance request, cascading cancellation, offering the
    /// waitlist) runs only while <c>ReturnedAt</c> is still unset, and the
    /// Membership-facing half (reliability reporting) runs only while
    /// <c>ReliabilityReportedAt</c> is still unset — each guarded
    /// independently rather than by one all-or-nothing flag, since a crash
    /// could land between the two.
    /// </summary>
    [Authorize(LendingPermissions.Loans.Return)]
    public virtual async Task<LoanDto> ReturnAsync(Guid loanId, RecordReturnDto input)
    {
        var loan = await _loanRepository.GetAsync(loanId);
        var at = Clock.Now;

        if (!loan.ReturnedAt.HasValue)
        {
            var maintenanceRequest = await _loanManager.ReturnAsync(loan, at, input.ReturnedCondition);
            await _loanRepository.UpdateAsync(loan, autoSave: true);

            if (maintenanceRequest is not null)
            {
                await _maintenanceRequestRepository.InsertAsync(maintenanceRequest, autoSave: true);
                await _toolInstanceCirculationReportingAppService.MarkReturnedForMaintenanceAsync(loan.ToolInstanceId, input.ReturnedCondition);
                await _reservationManager.CancelForMaintenanceAsync(loan.ToolInstanceId, at, "Instance placed under maintenance after a worsened return.");
            }
            else
            {
                await _toolInstanceCirculationReportingAppService.MarkReturnedAsync(loan.ToolInstanceId, input.ReturnedCondition);

                var rules = await _communityRulesLookupAppService.GetAsync();
                await _waitlistManager.OfferNextAsync(loan.ToolInstanceId, at, rules.WaitlistOfferWindowHours);
            }
        }

        if (!loan.ReliabilityReportedAt.HasValue)
        {
            await ReportReliabilityOutcomesAsync(loan);

            loan.MarkReliabilityReported(at);
            await _loanRepository.UpdateAsync(loan, autoSave: true);
        }

        return MapToDto(loan);
    }

    /// <summary>LOAN-05: one report per applicable outcome, `Loan.Id` as `OccurrenceId` for every one — no local retry wrapper (research R8, membership-consumption.md).</summary>
    private async Task ReportReliabilityOutcomesAsync(Loan loan)
    {
        var wasWorsened = loan.IsWorsened();

        if (loan.IsOverdue)
        {
            await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
            {
                MemberId = loan.MemberId,
                OutcomeType = ReliabilityOutcomeType.OverdueReturn,
                OccurrenceId = loan.Id
            });
        }

        if (wasWorsened)
        {
            await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
            {
                MemberId = loan.MemberId,
                OutcomeType = ReliabilityOutcomeType.DamagedReturn,
                OccurrenceId = loan.Id
            });
        }

        if (!loan.IsOverdue && !wasWorsened)
        {
            await _reliabilityReportingAppService.ReportAsync(new ReportReliabilityOutcomeDto
            {
                MemberId = loan.MemberId,
                OutcomeType = ReliabilityOutcomeType.CleanReturn,
                OccurrenceId = loan.Id
            });
        }
    }

    public virtual async Task<PagedResultDto<LoanDto>> GetListAsync(GetLoanListInput input)
    {
        var totalCount = await _loanRepository.GetCountAsync(input.MemberId, input.OnlyOverdue);
        var loans = await _loanRepository.GetPagedListAsync(
            input.MemberId,
            input.OnlyOverdue,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        return new PagedResultDto<LoanDto>(totalCount, loans.Select(MapToDto).ToList());
    }

    public virtual async Task<LoanDto> GetAsync(Guid id)
    {
        var loan = await _loanRepository.GetAsync(id);
        return MapToDto(loan);
    }

    private static LoanDto MapToDto(Loan loan) => new()
    {
        Id = loan.Id,
        ReservationId = loan.ReservationId,
        MemberId = loan.MemberId,
        ToolInstanceId = loan.ToolInstanceId,
        CheckedOutAt = loan.CheckedOutAt,
        PlannedReturnDate = loan.PlannedReturnDate,
        ConditionAtCheckout = loan.ConditionAtCheckout,
        ReturnedAt = loan.ReturnedAt,
        ReturnedCondition = loan.ReturnedCondition,
        IsOverdue = loan.IsOverdue
    };
}

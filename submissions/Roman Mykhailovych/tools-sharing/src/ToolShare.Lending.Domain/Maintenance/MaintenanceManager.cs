using System;
using System.Threading.Tasks;
using ToolShare.Catalog;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// The out-of-band report rules that need more than the entity itself
/// (008 MAINT-06/MAINT-07). Catalog facts — circulation state and current
/// condition — are supplied by the caller as plain values, exactly as
/// <c>ReservationManager</c> receives availability: this domain service never
/// calls another module. The decision itself is the pure
/// <see cref="EnsureCanReportOutOfBand"/>, unit-testable without a database.
/// </summary>
public class MaintenanceManager : DomainService
{
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;

    public MaintenanceManager(IMaintenanceRequestRepository maintenanceRequestRepository)
    {
        _maintenanceRequestRepository = maintenanceRequestRepository;
    }

    /// <summary>
    /// The friendly pre-check before an out-of-band report. The one-open-request
    /// index (MAINT-01) and Catalog's own guard (IR-09) remain the authorities
    /// under concurrency.
    /// </summary>
    public async Task<MaintenanceRequest> ReportOutOfBandAsync(
        Guid toolInstanceId,
        ToolInstanceCirculationState circulationState,
        ToolCondition currentCondition,
        ToolCondition observedCondition,
        string reason,
        Guid reportedByMemberId,
        DateTime at)
    {
        var hasOpenRequest = await _maintenanceRequestRepository.GetOpenForInstanceAsync(toolInstanceId) is not null;

        EnsureCanReportOutOfBand(circulationState, currentCondition, observedCondition, hasOpenRequest);

        return MaintenanceRequest.ReportOutOfBand(GuidGenerator.Create(), toolInstanceId, reportedByMemberId, reason, observedCondition, at);
    }

    /// <summary>MAINT-06/MAINT-07, checked in a fixed order so the first failure wins.</summary>
    public static void EnsureCanReportOutOfBand(
        ToolInstanceCirculationState circulationState,
        ToolCondition currentCondition,
        ToolCondition observedCondition,
        bool hasOpenRequest)
    {
        if (circulationState == ToolInstanceCirculationState.Retired)
        {
            throw new BusinessException(LendingDomainErrorCodes.InstanceRetired);
        }

        if (circulationState == ToolInstanceCirculationState.OnLoan)
        {
            // Damage on a loaned instance is recorded when it is returned (004 FR-014).
            throw new BusinessException(LendingDomainErrorCodes.InstanceOnLoanRecordAtReturn);
        }

        if (hasOpenRequest || circulationState == ToolInstanceCirculationState.UnderMaintenance)
        {
            throw new BusinessException(LendingDomainErrorCodes.MaintenanceRequestAlreadyOpen);
        }

        if ((int)observedCondition < (int)currentCondition)
        {
            throw new BusinessException(LendingDomainErrorCodes.ObservedConditionBetterThanCurrent)
                .WithData("current", currentCondition);
        }
    }
}

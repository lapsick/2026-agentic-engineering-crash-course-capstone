using System;
using ToolShare.Catalog;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// Repair tracking for an instance, opened by a worsened return **or** by an
/// out-of-band report (data-model.md `MAINT-01`/`MAINT-02`; 008 `MAINT-04`).
/// At most one <see cref="MaintenanceRequestStatus.Open"/> request exists per
/// instance at a time, whatever its origin — enforced by a filtered unique
/// index (the authority) with an application-layer pre-check for a friendly
/// message. Origin fields are written once, at creation.
/// </summary>
public class MaintenanceRequest : FullAuditedAggregateRoot<Guid>
{
    public virtual Guid ToolInstanceId { get; private set; }

    public virtual MaintenanceRequestOrigin Origin { get; private set; }

    /// <summary>Set iff <see cref="Origin"/> is <see cref="MaintenanceRequestOrigin.ReturnTriggered"/>.</summary>
    public virtual Guid? TriggeringLoanId { get; private set; }

    /// <summary>Membership member id of the reporting Librarian. Set iff <see cref="Origin"/> is <see cref="MaintenanceRequestOrigin.OutOfBand"/>.</summary>
    public virtual Guid? ReportedByMemberId { get; private set; }

    /// <summary>Set iff <see cref="Origin"/> is <see cref="MaintenanceRequestOrigin.OutOfBand"/>.</summary>
    public virtual string? ReportReason { get; private set; }

    /// <summary>Set iff <see cref="Origin"/> is <see cref="MaintenanceRequestOrigin.OutOfBand"/>.</summary>
    public virtual ToolCondition? ObservedCondition { get; private set; }

    public virtual MaintenanceRequestStatus Status { get; private set; }

    /// <summary>For an out-of-band request this is also the moment of the report.</summary>
    public virtual DateTime OpenedAt { get; private set; }

    public virtual DateTime? ClosedAt { get; private set; }

    public virtual decimal? Cost { get; private set; }

    protected MaintenanceRequest()
    {
    }

    /// <summary>A return-triggered request (004 FR-014). Signature unchanged by 008 (FR-022).</summary>
    public MaintenanceRequest(Guid id, Guid toolInstanceId, Guid triggeringLoanId, DateTime openedAt)
        : base(id)
    {
        ToolInstanceId = toolInstanceId;
        Origin = MaintenanceRequestOrigin.ReturnTriggered;
        TriggeringLoanId = triggeringLoanId;
        OpenedAt = openedAt;
        Status = MaintenanceRequestStatus.Open;
    }

    private MaintenanceRequest(Guid id, Guid toolInstanceId, Guid reportedByMemberId, string reportReason, ToolCondition observedCondition, DateTime openedAt)
        : base(id)
    {
        ToolInstanceId = toolInstanceId;
        Origin = MaintenanceRequestOrigin.OutOfBand;
        ReportedByMemberId = reportedByMemberId;
        ReportReason = reportReason;
        ObservedCondition = observedCondition;
        OpenedAt = openedAt;
        Status = MaintenanceRequestStatus.Open;
    }

    /// <summary>
    /// 008 MAINT-04/MAINT-05: an out-of-band request. Enforces only the
    /// reason; whether the instance may be reported at all (MAINT-06/MAINT-07)
    /// needs facts from other modules and is <see cref="MaintenanceManager"/>'s concern.
    /// </summary>
    public static MaintenanceRequest ReportOutOfBand(
        Guid id,
        Guid toolInstanceId,
        Guid reportedByMemberId,
        string reason,
        ToolCondition observedCondition,
        DateTime openedAt)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessException(LendingDomainErrorCodes.MaintenanceReasonRequired);
        }

        reason = reason.Trim();
        Check.Length(reason, nameof(reason), LendingDomainSharedConsts.MaintenanceReportReasonMaxLength);

        return new MaintenanceRequest(id, toolInstanceId, reportedByMemberId, reason, observedCondition, openedAt);
    }

    /// <summary>
    /// Enforces MAINT-02: permitted only while <see cref="MaintenanceRequestStatus.Open"/>;
    /// requires a non-negative cost — zero is a valid, explicit cost, distinct
    /// from <paramref name="cost"/> being omitted (<c>null</c>).
    /// </summary>
    public void Close(DateTime closedAt, decimal? cost)
    {
        if (Status != MaintenanceRequestStatus.Open)
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);
        }

        if (cost is null || cost < 0)
        {
            throw new BusinessException(LendingDomainErrorCodes.MaintenanceCostRequired);
        }

        Status = MaintenanceRequestStatus.Closed;
        ClosedAt = closedAt;
        Cost = cost;
    }
}

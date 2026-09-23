using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// Repair tracking for an instance, opened only by a worsened return
/// (data-model.md `MAINT-01`/`MAINT-02`). At most one <see cref="MaintenanceRequestStatus.Open"/>
/// request exists per instance at a time — enforced by a filtered unique
/// index (the authority) with an application-layer pre-check for a friendly
/// message.
/// </summary>
public class MaintenanceRequest : FullAuditedAggregateRoot<Guid>
{
    public virtual Guid ToolInstanceId { get; private set; }

    public virtual Guid TriggeringLoanId { get; private set; }

    public virtual MaintenanceRequestStatus Status { get; private set; }

    public virtual DateTime OpenedAt { get; private set; }

    public virtual DateTime? ClosedAt { get; private set; }

    public virtual decimal? Cost { get; private set; }

    protected MaintenanceRequest()
    {
    }

    public MaintenanceRequest(Guid id, Guid toolInstanceId, Guid triggeringLoanId, DateTime openedAt)
        : base(id)
    {
        ToolInstanceId = toolInstanceId;
        TriggeringLoanId = triggeringLoanId;
        OpenedAt = openedAt;
        Status = MaintenanceRequestStatus.Open;
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

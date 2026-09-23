using System;
using Volo.Abp.Domain.Entities;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// One row per state transition of a <see cref="ToolInstance"/> — registration,
/// condition change, and retirement all append exactly one row here.
/// Append-only (Constitution IV, HR-01): every property is assigned once in the
/// constructor with private setters; there is no update or delete path anywhere
/// in the domain, the repository, or the application layer (HR-02).
/// </summary>
public class ToolInstanceStateChange : Entity<Guid>
{
    public virtual Guid ToolInstanceId { get; private set; }

    /// <summary><c>null</c> only on the registration row (HR-03).</summary>
    public virtual ToolCondition? PreviousCondition { get; private set; }

    public virtual ToolCondition NewCondition { get; private set; }

    /// <summary><c>null</c> only on the registration row (HR-03).</summary>
    public virtual ToolInstanceCirculationState? PreviousCirculationState { get; private set; }

    public virtual ToolInstanceCirculationState NewCirculationState { get; private set; }

    public virtual string? Reason { get; private set; }

    public virtual DateTime ChangedAt { get; private set; }

    /// <summary>No FK to identity (Constitution III).</summary>
    public virtual Guid? ChangedByUserId { get; private set; }

    protected ToolInstanceStateChange()
    {
    }

    internal ToolInstanceStateChange(
        Guid id,
        Guid toolInstanceId,
        ToolCondition? previousCondition,
        ToolCondition newCondition,
        ToolInstanceCirculationState? previousCirculationState,
        ToolInstanceCirculationState newCirculationState,
        string? reason,
        DateTime changedAt,
        Guid? changedByUserId)
        : base(id)
    {
        ToolInstanceId = toolInstanceId;
        PreviousCondition = previousCondition;
        NewCondition = newCondition;
        PreviousCirculationState = previousCirculationState;
        NewCirculationState = newCirculationState;
        Reason = reason;
        ChangedAt = changedAt;
        ChangedByUserId = changedByUserId;
    }
}

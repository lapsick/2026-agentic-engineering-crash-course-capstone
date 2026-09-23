using System;
using Volo.Abp.Domain.Entities;

namespace ToolShare.Membership.Members;

/// <summary>
/// One row per standing transition of a <see cref="Member"/> — enrolment, status
/// change, role change, and rating outcome all append exactly one row here
/// (research R8, the same unified-table shape Catalog uses for
/// <c>ToolInstanceStateChange</c>). Append-only (Constitution IV, HR-01): every
/// property is assigned once in the constructor with private setters; there is
/// no update or delete path anywhere in the domain, the repository, or the
/// application layer (HR-02).
/// </summary>
public class MemberStandingChange : Entity<Guid>
{
    public virtual Guid MemberId { get; private set; }

    public virtual MemberStandingChangeKind Kind { get; private set; }

    public virtual DateTime ChangedAt { get; private set; }

    /// <summary>No FK to identity (Constitution III). Null for system-originated entries.</summary>
    public virtual Guid? ChangedByUserId { get; private set; }

    public virtual string? Reason { get; private set; }

    /// <summary><c>null</c> only on the enrolment row (HR-03).</summary>
    public virtual MembershipStatus? PreviousStatus { get; private set; }

    public virtual MembershipStatus? NewStatus { get; private set; }

    /// <summary><c>null</c> only on the enrolment row (HR-03).</summary>
    public virtual CommunityRole? PreviousRole { get; private set; }

    public virtual CommunityRole? NewRole { get; private set; }

    /// <summary>Required for <see cref="MemberStandingChangeKind.RatingOutcome"/>.</summary>
    public virtual ReliabilityOutcomeType? OutcomeType { get; private set; }

    /// <summary>The points the rules called for, before clamping.</summary>
    public virtual int? RawPoints { get; private set; }

    /// <summary>The points actually applied after clamping.</summary>
    public virtual int? EffectivePoints { get; private set; }

    /// <summary>0-100, the score after this entry. Only set for <see cref="MemberStandingChangeKind.RatingOutcome"/>.</summary>
    public virtual int? ResultingRating { get; private set; }

    /// <summary>The calling module's causing entity (loan, return); <c>null</c> for <see cref="ReliabilityOutcomeType.ManualAdjustment"/>.</summary>
    public virtual Guid? OccurrenceId { get; private set; }

    protected MemberStandingChange()
    {
    }

    internal MemberStandingChange(
        Guid id,
        Guid memberId,
        MemberStandingChangeKind kind,
        DateTime changedAt,
        Guid? changedByUserId,
        string? reason,
        MembershipStatus? previousStatus,
        MembershipStatus? newStatus,
        CommunityRole? previousRole,
        CommunityRole? newRole,
        ReliabilityOutcomeType? outcomeType,
        int? rawPoints,
        int? effectivePoints,
        int? resultingRating,
        Guid? occurrenceId)
        : base(id)
    {
        MemberId = memberId;
        Kind = kind;
        ChangedAt = changedAt;
        ChangedByUserId = changedByUserId;
        Reason = reason;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        PreviousRole = previousRole;
        NewRole = newRole;
        OutcomeType = outcomeType;
        RawPoints = rawPoints;
        EffectivePoints = effectivePoints;
        ResultingRating = resultingRating;
        OccurrenceId = occurrenceId;
    }
}

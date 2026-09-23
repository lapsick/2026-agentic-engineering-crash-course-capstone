using System;
using System.Collections.Generic;
using ToolShare.Membership.CommunityRules;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Membership.Members;

/// <summary>
/// A member of the community — its own aggregate root because it has its own
/// lifecycle and append-only history and is the thing downstream modules
/// (Lending) will reference by id. Deliberately carries no relationship to
/// <see cref="CommunityRules.CommunityRules"/>: the effective concurrent-loan
/// limit (<see cref="EffectiveConcurrentLoanLimit"/>) is computed at read time
/// so a rules change applies to every member immediately, with zero writes.
/// </summary>
public class Member : FullAuditedAggregateRoot<Guid>
{
    public virtual Guid IdentityUserId { get; private set; }

    public virtual string DisplayName { get; private set; } = null!;

    public virtual string Email { get; private set; } = null!;

    public virtual MembershipStatus Status { get; private set; }

    public virtual CommunityRole Role { get; private set; }

    public virtual int CurrentRating { get; private set; }

    public virtual DateTime EnrolledAt { get; private set; }

    public virtual DateTime? StatusChangedAt { get; private set; }

    public virtual string? StatusChangeReason { get; private set; }

    public virtual ICollection<MemberStandingChange> StandingHistory { get; protected set; }

    /// <summary>
    /// Derived, not persisted. Membership's own notion of standing — says
    /// nothing about loans, deliberately, mirroring how Catalog's
    /// <c>ToolInstance.IsAvailable</c> says nothing about lending.
    /// </summary>
    public virtual bool IsActive => Status == MembershipStatus.Active;

    protected Member()
    {
        StandingHistory = new List<MemberStandingChange>();
    }

    /// <summary>
    /// Enrols a new member. Enforces MR-03: status starts <see cref="MembershipStatus.Active"/>,
    /// role starts <see cref="CommunityRole.Member"/>, rating starts at 100, and
    /// the first history row is appended with both Previous* dimensions null
    /// (FR-003, FR-017). Identity uniqueness (MR-02) is checked by the caller
    /// (<see cref="MemberManager"/>), which has repository access.
    /// </summary>
    public Member(Guid id, Guid identityUserId, string displayName, string email, DateTime enrolledAt, Guid? enrolledByUserId)
        : base(id)
    {
        StandingHistory = new List<MemberStandingChange>();

        IdentityUserId = identityUserId;
        SetDisplayName(displayName);
        SetEmail(email);

        Status = MembershipStatus.Active;
        Role = CommunityRole.Member;
        CurrentRating = MembershipDomainSharedConsts.DefaultRating;
        EnrolledAt = enrolledAt;

        AppendStandingChange(
            MemberStandingChangeKind.Enrolled,
            enrolledAt,
            enrolledByUserId,
            reason: null,
            previousStatus: null,
            newStatus: Status,
            previousRole: null,
            newRole: Role,
            previousRating: null,
            newRating: CurrentRating,
            outcomeType: null,
            rawPoints: null,
            effectivePoints: null,
            occurrenceId: null,
            communityRules: null);
    }

    /// <summary>Enforces MR-01: required, trimmed, length-bounded.</summary>
    public void SetDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessException(MembershipDomainErrorCodes.DisplayNameRequired);
        }

        displayName = displayName.Trim();
        Check.Length(
            displayName,
            nameof(displayName),
            MembershipDomainSharedConsts.DisplayNameMaxLength,
            MembershipDomainSharedConsts.DisplayNameMinLength);

        DisplayName = displayName;
    }

    /// <summary>Required, length-bounded, stored lower-cased.</summary>
    public void SetEmail(string email)
    {
        email = Check.NotNullOrWhiteSpace(email, nameof(email)).Trim();
        Check.Length(email, nameof(email), MembershipDomainSharedConsts.EmailMaxLength);

        Email = email.ToLowerInvariant();
    }

    /// <summary>
    /// Enforces MR-04: rejected if already deactivated; a reason is required.
    /// The last-active-Administrator guard (MR-10) is enforced by the caller
    /// (<see cref="MemberManager"/>), which has repository access.
    /// </summary>
    public void Deactivate(string reason, DateTime at, Guid? byUserId, CommunityRules.CommunityRules? communityRules = null)
    {
        if (Status == MembershipStatus.Deactivated)
        {
            throw new BusinessException(MembershipDomainErrorCodes.AlreadyDeactivated);
        }

        reason = Check.NotNullOrWhiteSpace(reason, nameof(reason)).Trim();
        Check.Length(reason, nameof(reason), MembershipDomainSharedConsts.StatusChangeReasonMaxLength);

        var previousStatus = Status;
        Status = MembershipStatus.Deactivated;
        StatusChangedAt = at;
        StatusChangeReason = reason;

        AppendStandingChange(
            MemberStandingChangeKind.StatusChanged,
            at,
            byUserId,
            reason,
            previousStatus,
            Status,
            previousRole: Role,
            newRole: Role,
            previousRating: CurrentRating,
            newRating: CurrentRating,
            outcomeType: null,
            rawPoints: null,
            effectivePoints: null,
            occurrenceId: null,
            communityRules: communityRules);
    }

    /// <summary>
    /// Enforces MR-04: rejected if already active. Never touches
    /// <see cref="CurrentRating"/> (FR-005) — a reactivated member keeps the
    /// rating they had when deactivated.
    /// </summary>
    public void Reactivate(DateTime at, Guid? byUserId, CommunityRules.CommunityRules? communityRules = null)
    {
        if (Status == MembershipStatus.Active)
        {
            throw new BusinessException(MembershipDomainErrorCodes.AlreadyActive);
        }

        var previousStatus = Status;
        Status = MembershipStatus.Active;
        StatusChangedAt = at;
        StatusChangeReason = null;

        AppendStandingChange(
            MemberStandingChangeKind.StatusChanged,
            at,
            byUserId,
            reason: null,
            previousStatus,
            Status,
            previousRole: Role,
            newRole: Role,
            previousRating: CurrentRating,
            newRating: CurrentRating,
            outcomeType: null,
            rawPoints: null,
            effectivePoints: null,
            occurrenceId: null,
            communityRules: communityRules);
    }

    /// <summary>
    /// Enforces MR-05: rejected as a same-value no-op. Exactly one role is
    /// held at all times (FR-004). The last-active-Administrator guard
    /// (MR-10) is enforced by the caller (<see cref="MemberManager"/>).
    /// </summary>
    public void ChangeRole(CommunityRole newRole, DateTime at, Guid? byUserId, CommunityRules.CommunityRules? communityRules = null)
    {
        if (newRole == Role)
        {
            throw new BusinessException(MembershipDomainErrorCodes.RoleUnchanged);
        }

        var previousRole = Role;
        Role = newRole;

        AppendStandingChange(
            MemberStandingChangeKind.RoleChanged,
            at,
            byUserId,
            reason: null,
            previousStatus: Status,
            newStatus: Status,
            previousRole,
            newRole,
            previousRating: CurrentRating,
            newRating: CurrentRating,
            outcomeType: null,
            rawPoints: null,
            effectivePoints: null,
            occurrenceId: null,
            communityRules: communityRules);
    }

    /// <summary>
    /// Enforces MR-06: computes <c>effectivePoints = Clamp(CurrentRating + rawPoints, 0, 100) - CurrentRating</c>,
    /// applies it to <see cref="CurrentRating"/>, and appends one
    /// <see cref="MemberStandingChangeKind.RatingOutcome"/> entry recording
    /// both <paramref name="rawPoints"/> and the clamped effective points plus
    /// the resulting score (FR-016, FR-017). Deliberately permitted on a
    /// deactivated member — deactivation blocks access, not bookkeeping (e.g.
    /// an overdue tool returned after the member left must still move their
    /// rating).
    /// </summary>
    public void ApplyOutcome(
        ReliabilityOutcomeType type,
        int rawPoints,
        Guid? occurrenceId,
        string? reason,
        DateTime at,
        Guid? byUserId,
        CommunityRules.CommunityRules? communityRules = null)
    {
        if (type == ReliabilityOutcomeType.ManualAdjustment && string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessException(MembershipDomainErrorCodes.AdjustmentReasonRequired);
        }

        reason = reason?.Trim();
        Check.Length(reason, nameof(reason), MembershipDomainSharedConsts.AdjustmentReasonMaxLength);

        var previousRating = CurrentRating;
        var clampedRating = Math.Clamp(previousRating + rawPoints, MembershipDomainSharedConsts.MinRating, MembershipDomainSharedConsts.MaxRating);
        var effectivePoints = clampedRating - previousRating;

        CurrentRating = clampedRating;

        AppendStandingChange(
            MemberStandingChangeKind.RatingOutcome,
            at,
            byUserId,
            reason,
            previousStatus: Status,
            newStatus: Status,
            previousRole: Role,
            newRole: Role,
            previousRating,
            newRating: CurrentRating,
            outcomeType: type,
            rawPoints,
            effectivePoints,
            occurrenceId,
            communityRules: communityRules);
    }

    /// <summary>
    /// Enforces MR-08 — a pure function on the aggregate, not a stored column,
    /// so a rules change applies to every member immediately with zero writes
    /// (FR-023).
    /// </summary>
    public int EffectiveConcurrentLoanLimit(CommunityRules.CommunityRules communityRules)
    {
        return communityRules.ComputeEffectiveConcurrentLoanLimit(CurrentRating);
    }

    /// <summary>
    /// The single private helper every transition method funnels through
    /// (MR-07): appends the append-only history row and raises
    /// <see cref="Members.MemberStandingChangedEto"/> from the same place, so
    /// the two can never diverge (FR-017a, HR-04). <paramref name="communityRules"/>
    /// is optional and used only to compute <c>CrossedLowRatingThreshold</c> on
    /// the event — Member itself holds no relationship to CommunityRules
    /// (data-model.md "Aggregate overview"); callers that have the current
    /// rules in hand (US5's reliability reporting) pass them through.
    /// </summary>
    private void AppendStandingChange(
        MemberStandingChangeKind kind,
        DateTime changedAt,
        Guid? changedByUserId,
        string? reason,
        MembershipStatus? previousStatus,
        MembershipStatus newStatus,
        CommunityRole? previousRole,
        CommunityRole newRole,
        int? previousRating,
        int newRating,
        ReliabilityOutcomeType? outcomeType,
        int? rawPoints,
        int? effectivePoints,
        Guid? occurrenceId,
        CommunityRules.CommunityRules? communityRules)
    {
        var resultingRating = kind == MemberStandingChangeKind.RatingOutcome ? (int?)newRating : null;

        StandingHistory.Add(new MemberStandingChange(
            Guid.NewGuid(),
            Id,
            kind,
            changedAt,
            changedByUserId,
            reason,
            previousStatus,
            newStatus,
            previousRole,
            newRole,
            outcomeType,
            rawPoints,
            effectivePoints,
            resultingRating,
            occurrenceId));

        var crossedLowRatingThreshold = false;
        if (communityRules is not null && previousRating.HasValue)
        {
            var threshold = communityRules.LowRatingThreshold;
            crossedLowRatingThreshold = (previousRating.Value >= threshold) != (newRating >= threshold);
        }

        AddLocalEvent(new MemberStandingChangedEto
        {
            MemberId = Id,
            IdentityUserId = IdentityUserId,
            Kind = kind,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            PreviousRole = previousRole,
            NewRole = newRole,
            PreviousRating = previousRating,
            NewRating = newRating,
            CrossedLowRatingThreshold = crossedLowRatingThreshold,
            OutcomeType = outcomeType,
            OccurrenceId = occurrenceId,
            Reason = reason,
            ChangedAt = changedAt,
            ChangedByUserId = changedByUserId
        });
    }
}

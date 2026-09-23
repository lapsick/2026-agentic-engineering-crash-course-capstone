using System;
using Volo.Abp.Application.Dtos;

namespace ToolShare.Membership.Members;

/// <summary>
/// One row of a member's append-only standing history (HR-01..HR-06), shaped
/// for both <see cref="IMemberAppService"/> (US4, Phase 6) and
/// <see cref="IMyMembershipAppService"/> (US3) — the same row, read either by
/// an administrator about someone else or by a member about themselves.
/// <para>
/// <b>Note</b>: created ahead of its originally scheduled task (T092, US4)
/// because <see cref="IMyMembershipAppService.GetStandingHistoryAsync"/>
/// (T083, US3) already returns <c>List&lt;MemberStandingChangeDto&gt;</c> per
/// contracts/membership-app-services.md — the contract couples the two
/// stories at the DTO level even though the interfaces land in different
/// phases. T093 (adding <c>GetStandingHistoryAsync</c> to
/// <see cref="IMemberAppService"/>) reuses this same type rather than
/// introducing a second one.
/// </para>
/// </summary>
public class MemberStandingChangeDto : EntityDto<Guid>
{
    public MemberStandingChangeKind Kind { get; set; }

    public DateTime ChangedAt { get; set; }

    public Guid? ChangedByUserId { get; set; }

    /// <summary>
    /// Resolved from the Membership roster (the actor is always a member),
    /// never from the identity store — Membership must not reference
    /// Identity. Null for system-originated entries.
    /// </summary>
    public string? ChangedByDisplayName { get; set; }

    public string? Reason { get; set; }

    public MembershipStatus? PreviousStatus { get; set; }

    public MembershipStatus? NewStatus { get; set; }

    public CommunityRole? PreviousRole { get; set; }

    public CommunityRole? NewRole { get; set; }

    public ReliabilityOutcomeType? OutcomeType { get; set; }

    public int? RawPoints { get; set; }

    public int? EffectivePoints { get; set; }

    public int? ResultingRating { get; set; }

    public Guid? OccurrenceId { get; set; }
}

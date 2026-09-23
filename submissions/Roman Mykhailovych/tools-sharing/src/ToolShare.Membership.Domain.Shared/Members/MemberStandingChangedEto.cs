using System;

namespace ToolShare.Membership.Members;

/// <summary>
/// Raised whenever a member's standing changes: enrolment, status change,
/// role change, or a rating outcome. Lives in Domain.Shared (not
/// Application.Contracts) because Domain raises it via <c>AddLocalEvent</c>
/// and standard ABP layering does not let Domain reference
/// Application.Contracts — the same correction 002 made for
/// <c>ToolInstanceStateChangedEto</c>. Still part of the Tier 1 public
/// boundary: Application.Contracts re-exposes it transitively.
/// </summary>
[Serializable]
public class MemberStandingChangedEto
{
    public Guid MemberId { get; set; }
    public Guid IdentityUserId { get; set; }

    public MemberStandingChangeKind Kind { get; set; }

    public MembershipStatus? PreviousStatus { get; set; }
    public MembershipStatus NewStatus { get; set; }

    public CommunityRole? PreviousRole { get; set; }
    public CommunityRole NewRole { get; set; }

    public int? PreviousRating { get; set; }
    public int NewRating { get; set; }

    /// <summary>
    /// True when this change moved the rating across CommunityRules.LowRatingThreshold
    /// in either direction — the signal that a member's borrowing allowance changed.
    /// </summary>
    public bool CrossedLowRatingThreshold { get; set; }

    public ReliabilityOutcomeType? OutcomeType { get; set; }
    public Guid? OccurrenceId { get; set; }

    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } // UTC
    public Guid? ChangedByUserId { get; set; }
}

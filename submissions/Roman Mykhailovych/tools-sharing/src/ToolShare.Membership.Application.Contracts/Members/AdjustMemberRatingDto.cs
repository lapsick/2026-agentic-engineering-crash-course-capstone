using System.ComponentModel.DataAnnotations;

namespace ToolShare.Membership.Members;

/// <summary>
/// FR-021: the only input that creates a <see cref="ReliabilityOutcomeType.ManualAdjustment"/>
/// entry. <see cref="Reason"/> is mandatory here — unlike automatic outcomes,
/// where it is optional — because a correction must always explain itself.
/// <see cref="Points"/> is a caller-supplied signed value (not derived from
/// <c>CommunityRules</c>) and is clamped exactly like automatic outcomes
/// (MR-06).
/// </summary>
public class AdjustMemberRatingDto
{
    [Range(-100, 100)]
    public int Points { get; set; }

    [Required]
    [StringLength(512)]
    public string Reason { get; set; } = default!;

    [Required]
    public string ConcurrencyStamp { get; set; } = default!;
}

using System.ComponentModel.DataAnnotations;

namespace ToolShare.Membership.CommunityRules;

/// <summary>
/// Data annotations catch the per-field ranges (FR-014). The cross-field rule
/// (<c>ReducedConcurrentLoanLimit &lt;= ConcurrentLoanLimit</c>) is enforced by
/// the domain in <see cref="CommunityRules.Update"/> (CRR-01) — it is not
/// re-validated here, since it is a rule about the community, not about the
/// request shape, and must hold on every path.
/// </summary>
public class UpdateCommunityRulesDto
{
    [Range(1, 365)]
    public int MaxLoanTermDays { get; set; }

    [Range(1, 100)]
    public int ConcurrentLoanLimit { get; set; }

    [Range(0, 100)]
    public int LowRatingThreshold { get; set; }

    [Range(1, 100)]
    public int ReducedConcurrentLoanLimit { get; set; }

    [Range(0, 100)]
    public int OverduePenaltyPoints { get; set; }

    [Range(0, 100)]
    public int DamagePenaltyPoints { get; set; }

    [Range(0, 100)]
    public int CleanReturnRewardPoints { get; set; }

    [Range(1, 8760)]
    public int WaitlistOfferWindowHours { get; set; }

    [Range(1, 365)]
    public int ReminderLeadTimeDays { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = default!;
}

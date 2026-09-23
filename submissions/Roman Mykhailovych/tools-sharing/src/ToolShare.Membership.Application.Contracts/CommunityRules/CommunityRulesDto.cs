using System;

namespace ToolShare.Membership.CommunityRules;

/// <summary>
/// The Tier 1 public projection of the community rules (contracts/membership-public-contracts.md).
/// Read-only by design: mutation lives on the internal <see cref="ICommunityRulesAppService"/>
/// and is Administrator-only — a downstream module must never be able to
/// rewrite the community's rules.
/// </summary>
public class CommunityRulesDto
{
    public int MaxLoanTermDays { get; set; }

    public int ConcurrentLoanLimit { get; set; }

    public int LowRatingThreshold { get; set; }

    public int ReducedConcurrentLoanLimit { get; set; }

    public int OverduePenaltyPoints { get; set; }

    public int DamagePenaltyPoints { get; set; }

    public int CleanReturnRewardPoints { get; set; }

    public int WaitlistOfferWindowHours { get; set; }

    public int ReminderLeadTimeDays { get; set; }

    /// <summary>Null on a fresh installation: nobody has changed the rules since the seeder inserted them.</summary>
    public DateTime? LastChangedAt { get; set; }

    /// <summary>Null on a fresh installation. The identity user id of the last editor.</summary>
    public Guid? LastChangedByUserId { get; set; }
}

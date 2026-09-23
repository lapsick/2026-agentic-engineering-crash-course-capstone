using System;

namespace ToolShare.Membership;

/// <summary>
/// The well-known single row id for <c>CommunityRules</c> (CRR-02) and the
/// default values from the spec's Assumptions, used by both the seeder and
/// its idempotence tests.
/// </summary>
public static class CommunityRulesConsts
{
    /// <summary>The only permitted <c>CommunityRules.Id</c> value.</summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-0000000000c1");

    public const int DefaultMaxLoanTermDays = 14;
    public const int DefaultConcurrentLoanLimit = 3;
    public const int DefaultLowRatingThreshold = 50;
    public const int DefaultReducedConcurrentLoanLimit = 1;
    public const int DefaultOverduePenaltyPoints = 10;
    public const int DefaultDamagePenaltyPoints = 20;
    public const int DefaultCleanReturnRewardPoints = 2;
    public const int DefaultWaitlistOfferWindowHours = 24;
    public const int DefaultReminderLeadTimeDays = 2;
}

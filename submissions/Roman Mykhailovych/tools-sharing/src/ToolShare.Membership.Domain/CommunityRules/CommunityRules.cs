using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Membership.CommunityRules;

/// <summary>
/// A single-row aggregate root holding every configurable community rule
/// (research R6): audit properties, a concurrency stamp and atomic cross-field
/// validation are all things ABP's setting system cannot give this feature, so
/// it is a normal aggregate rather than a <c>SettingDefinitionProvider</c>.
/// Exactly one row exists, at the well-known id <see cref="Membership.CommunityRulesConsts.SingletonId"/>
/// (CRR-02).
/// </summary>
public class CommunityRules : FullAuditedAggregateRoot<Guid>
{
    public virtual int MaxLoanTermDays { get; private set; }

    public virtual int ConcurrentLoanLimit { get; private set; }

    public virtual int LowRatingThreshold { get; private set; }

    public virtual int ReducedConcurrentLoanLimit { get; private set; }

    public virtual int OverduePenaltyPoints { get; private set; }

    public virtual int DamagePenaltyPoints { get; private set; }

    public virtual int CleanReturnRewardPoints { get; private set; }

    public virtual int WaitlistOfferWindowHours { get; private set; }

    public virtual int ReminderLeadTimeDays { get; private set; }

    protected CommunityRules()
    {
    }

    /// <summary>Constructs the singleton with the spec's documented defaults (CRR-02).</summary>
    public CommunityRules(Guid id)
        : base(id)
    {
        Update(
            Membership.CommunityRulesConsts.DefaultMaxLoanTermDays,
            Membership.CommunityRulesConsts.DefaultConcurrentLoanLimit,
            Membership.CommunityRulesConsts.DefaultLowRatingThreshold,
            Membership.CommunityRulesConsts.DefaultReducedConcurrentLoanLimit,
            Membership.CommunityRulesConsts.DefaultOverduePenaltyPoints,
            Membership.CommunityRulesConsts.DefaultDamagePenaltyPoints,
            Membership.CommunityRulesConsts.DefaultCleanReturnRewardPoints,
            Membership.CommunityRulesConsts.DefaultWaitlistOfferWindowHours,
            Membership.CommunityRulesConsts.DefaultReminderLeadTimeDays);
    }

    /// <summary>
    /// Enforces CRR-01: every value is validated together; a single invalid
    /// field rejects the whole change with a <see cref="BusinessException"/>
    /// naming it via the <c>rule</c> data key (FR-014). The cross-field rule
    /// (<see cref="ReducedConcurrentLoanLimit"/> &lt;= <see cref="ConcurrentLoanLimit"/>)
    /// is why this is one entity rather than N settings.
    /// </summary>
    public void Update(
        int maxLoanTermDays,
        int concurrentLoanLimit,
        int lowRatingThreshold,
        int reducedConcurrentLoanLimit,
        int overduePenaltyPoints,
        int damagePenaltyPoints,
        int cleanReturnRewardPoints,
        int waitlistOfferWindowHours,
        int reminderLeadTimeDays)
    {
        if (maxLoanTermDays <= 0)
        {
            throw InvalidRule(nameof(MaxLoanTermDays));
        }

        if (concurrentLoanLimit <= 0)
        {
            throw InvalidRule(nameof(ConcurrentLoanLimit));
        }

        if (lowRatingThreshold is < 0 or > 100)
        {
            throw InvalidRule(nameof(LowRatingThreshold));
        }

        if (reducedConcurrentLoanLimit <= 0 || reducedConcurrentLoanLimit > concurrentLoanLimit)
        {
            throw InvalidRule(nameof(ReducedConcurrentLoanLimit));
        }

        if (overduePenaltyPoints < 0)
        {
            throw InvalidRule(nameof(OverduePenaltyPoints));
        }

        if (damagePenaltyPoints < 0)
        {
            throw InvalidRule(nameof(DamagePenaltyPoints));
        }

        if (cleanReturnRewardPoints < 0)
        {
            throw InvalidRule(nameof(CleanReturnRewardPoints));
        }

        if (waitlistOfferWindowHours <= 0)
        {
            throw InvalidRule(nameof(WaitlistOfferWindowHours));
        }

        if (reminderLeadTimeDays <= 0)
        {
            throw InvalidRule(nameof(ReminderLeadTimeDays));
        }

        MaxLoanTermDays = maxLoanTermDays;
        ConcurrentLoanLimit = concurrentLoanLimit;
        LowRatingThreshold = lowRatingThreshold;
        ReducedConcurrentLoanLimit = reducedConcurrentLoanLimit;
        OverduePenaltyPoints = overduePenaltyPoints;
        DamagePenaltyPoints = damagePenaltyPoints;
        CleanReturnRewardPoints = cleanReturnRewardPoints;
        WaitlistOfferWindowHours = waitlistOfferWindowHours;
        ReminderLeadTimeDays = reminderLeadTimeDays;
    }

    /// <summary>
    /// MR-08's formula, as a pure function of a rating value rather than a
    /// <see cref="Members.Member"/> instance — lets a caller holding only a
    /// cached rating (no full aggregate load) still compute the same result
    /// <see cref="Members.Member.EffectiveConcurrentLoanLimit"/> delegates to
    /// this for, so the two never diverge.
    /// </summary>
    public int ComputeEffectiveConcurrentLoanLimit(int currentRating)
    {
        return currentRating >= LowRatingThreshold ? ConcurrentLoanLimit : ReducedConcurrentLoanLimit;
    }

    private static BusinessException InvalidRule(string ruleName)
    {
        return new BusinessException(MembershipDomainErrorCodes.InvalidRule).WithData("rule", ruleName);
    }
}

namespace ToolShare.Membership;

/// <summary>
/// The kinds of event that move a member's reliability rating. New outcome
/// types will be appended at higher numeric values; consumers must <c>switch</c>
/// with a <c>default</c> arm, per the compatibility rule 002 established.
/// </summary>
public enum ReliabilityOutcomeType
{
    /// <summary>Negative; magnitude from <c>CommunityRules.OverduePenaltyPoints</c>.</summary>
    OverdueReturn = 0,

    /// <summary>Negative; magnitude from <c>CommunityRules.DamagePenaltyPoints</c>.</summary>
    DamagedReturn = 1,

    /// <summary>Positive; magnitude from <c>CommunityRules.CleanReturnRewardPoints</c>.</summary>
    CleanReturn = 2,

    /// <summary>Signed; supplied by the Administrator, with a mandatory reason.</summary>
    ManualAdjustment = 3
}

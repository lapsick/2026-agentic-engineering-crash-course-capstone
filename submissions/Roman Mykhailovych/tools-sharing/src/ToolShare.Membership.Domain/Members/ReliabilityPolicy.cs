using System;
using ToolShare.Membership.CommunityRules;

namespace ToolShare.Membership.Members;

/// <summary>
/// Pure function mapping <see cref="ReliabilityOutcomeType"/> and the current
/// <see cref="CommunityRules"/> to signed raw points (before clamping). Free of
/// EF Core and ABP infrastructure so the whole rating algorithm is
/// unit-testable with no database (Constitution V).
/// </summary>
public static class ReliabilityPolicy
{
    public static int GetRawPoints(ReliabilityOutcomeType outcomeType, CommunityRules.CommunityRules communityRules, int? manualPoints = null)
    {
        return outcomeType switch
        {
            ReliabilityOutcomeType.OverdueReturn => -communityRules.OverduePenaltyPoints,
            ReliabilityOutcomeType.DamagedReturn => -communityRules.DamagePenaltyPoints,
            ReliabilityOutcomeType.CleanReturn => communityRules.CleanReturnRewardPoints,
            ReliabilityOutcomeType.ManualAdjustment => manualPoints
                ?? throw new ArgumentException("Manual adjustments require an explicit signed point value.", nameof(manualPoints)),
            _ => throw new ArgumentOutOfRangeException(nameof(outcomeType), outcomeType, null)
        };
    }
}

using System;

namespace ToolShare.Membership.Members;

/// <summary>
/// The <c>IDistributedCache&lt;MemberStandingCacheItem&gt;</c> payload behind
/// <see cref="MemberStandingProvider"/>, keyed by identity user id (see
/// <see cref="MemberStandingProvider.GetCacheKey"/>). Distinguishes "no member
/// record at all" (<see cref="IsEnrolled"/> false) from "enrolled but
/// deactivated" (<see cref="IsEnrolled"/> true, <see cref="IsActive"/> false) —
/// the same two-flag shape the public <c>IMemberStandingAppService</c> contract
/// (US5) will expose.
/// </summary>
[Serializable]
public class MemberStandingCacheItem
{
    public bool IsEnrolled { get; set; }

    public Guid? MemberId { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Carried alongside the gate's own minimal fields so
    /// <c>MemberStandingAppService.GetByIdentityUserIdAsync</c> (US5, FR-024's
    /// perf goal) can build the full public <c>MemberStandingDto</c> from this
    /// single cached read, with no second (member-by-id) database round trip
    /// on the warm path.
    /// </summary>
    public string? DisplayName { get; set; }

    public MembershipStatus? Status { get; set; }

    public CommunityRole? Role { get; set; }

    public int CurrentRating { get; set; }
}

using System;
using System.Threading.Tasks;

namespace ToolShare.Membership.Members;

/// <summary>
/// The O(1), no-DB-round-trip standing read the enrolment gate (research R3)
/// checks on every application-service call. Backed by
/// <c>IDistributedCache&lt;MemberStandingCacheItem&gt;</c> keyed by identity user
/// id, and kept fresh by <c>MemberStandingCacheInvalidator</c>, which evicts the
/// entry inside the same unit of work that recorded a standing change — so a
/// deactivation is visible on the caller's very next action (FR-006, FR-006a).
/// </summary>
public interface IMemberStandingProvider
{
    /// <summary>Returns <c>null</c> when <paramref name="identityUserId"/> has no member record (never throws).</summary>
    Task<MemberStandingSnapshot?> GetByIdentityUserIdAsync(Guid identityUserId);
}

/// <summary>A minimal, cache-friendly snapshot of a member's standing.</summary>
public class MemberStandingSnapshot
{
    public Guid MemberId { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Carried through so <c>MemberStandingAppService</c> (US5) can build its public DTO from this single cached read alone.</summary>
    public string? DisplayName { get; set; }

    public MembershipStatus? Status { get; set; }

    public CommunityRole? Role { get; set; }

    public int CurrentRating { get; set; }
}

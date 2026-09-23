using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ToolShare.Membership.Members;

/// <summary>
/// Read-only standing lookup for other modules (Tier 1, contracts/membership-public-contracts.md).
/// Consumers depend on this interface and its DTOs only — never on
/// Membership's <c>Domain</c> or <c>EntityFrameworkCore</c> layer.
/// </summary>
public interface IMemberStandingAppService : IApplicationService
{
    /// <summary>Never <c>null</c>, never throws for "not found" — an unknown identity yields <see cref="MemberStandingDto.IsEnrolled"/> false (FR-025).</summary>
    Task<MemberStandingDto> GetByIdentityUserIdAsync(Guid identityUserId);

    /// <summary>Same contract, keyed by member id.</summary>
    Task<MemberStandingDto> GetAsync(Guid memberId);

    /// <summary>Batch form. Unknown ids are omitted — no exception, no <c>null</c> entries. Order unspecified. Empty input yields an empty list.</summary>
    Task<List<MemberStandingDto>> GetByIdsAsync(IEnumerable<Guid> memberIds);
}

/// <summary>
/// The two-flag design (FR-025): <see cref="IsEnrolled"/> distinguishes "no
/// member record at all" from <see cref="IsActive"/>'s "enrolled but
/// deactivated", so a consumer needing the common check can test
/// <c>!IsActive</c> alone while one needing to tell the two rejection reasons
/// apart still can. When <see cref="IsEnrolled"/> is <c>false</c>, every
/// nullable field is <c>null</c> and both numeric fields are <c>0</c>.
/// </summary>
public class MemberStandingDto
{
    public bool IsEnrolled { get; set; }

    public Guid? MemberId { get; set; }

    public Guid? IdentityUserId { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>
    /// NEW (005-notifications, research R1): additive — populated by
    /// <c>GetAsync</c>/<c>GetByIdsAsync</c>, both of which already load the
    /// full <c>Member</c> entity. Deliberately left unpopulated by
    /// <c>GetByIdentityUserIdAsync</c>, which is backed by the cached,
    /// identity-keyed snapshot the enrolment gate's hot path uses — no
    /// current caller of that path needs email, so its cache entry shape is
    /// left untouched (contracts/membership-extension.md).
    /// </summary>
    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public MembershipStatus? Status { get; set; }

    public CommunityRole? Role { get; set; }

    public int CurrentRating { get; set; }

    /// <summary>
    /// Membership's own notion of allowance — how many concurrent loans the
    /// community's rules permit this person — deliberately silent about how
    /// many loans they currently hold or whether any is overdue (Lending's
    /// concern, not Membership's).
    /// </summary>
    public int EffectiveConcurrentLoanLimit { get; set; }
}

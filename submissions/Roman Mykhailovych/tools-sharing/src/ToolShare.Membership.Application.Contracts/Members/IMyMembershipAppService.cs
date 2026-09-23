using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ToolShare.Membership.Members;

/// <summary>
/// Self-service (US3, FR-022). Deliberately takes **no member id** on any
/// method: the caller's own <see cref="Members.Member"/> is resolved from
/// <c>CurrentUser.GetId()</c> by the implementation, so "view someone else's
/// standing" is not an authorization check that could be got wrong — it is an
/// operation this interface does not offer at all (SC-004). A member who
/// needs another member's data must go through <see cref="IMemberAppService"/>,
/// which requires <c>Membership.Members</c>.
/// <para>
/// There is deliberately no mutating operation here (US3 scenario 5): nothing
/// lets a member change their own status, role, or rating.
/// </para>
/// </summary>
public interface IMyMembershipAppService : IApplicationService
{
    Task<MyMembershipDto> GetAsync();

    Task<List<MemberStandingChangeDto>> GetStandingHistoryAsync();
}

/// <summary>
/// A member's own status, role, rating, and effective concurrent-loan limit
/// (FR-022, FR-023). Carries no <c>ConcurrencyStamp</c> — this surface offers
/// nothing to write back.
/// </summary>
public class MyMembershipDto
{
    public Guid MemberId { get; set; }

    public string DisplayName { get; set; } = default!;

    public MembershipStatus Status { get; set; }

    public CommunityRole Role { get; set; }

    public int CurrentRating { get; set; }

    public int EffectiveConcurrentLoanLimit { get; set; }

    public DateTime EnrolledAt { get; set; }
}

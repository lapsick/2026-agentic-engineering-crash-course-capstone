using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ToolShare.Membership.Members;

namespace ToolShare.Notifications;

/// <summary>
/// Test-only decorator (US2, FR-009) — strips <see cref="MemberStandingDto.Email"/>
/// for <see cref="NotificationsTestPrincipals.NoEmailUserId"/> specifically,
/// so tests can exercise "member has no email on file" without violating the
/// real invariant Membership's domain layer enforces (every enrolled member
/// always has a non-blank email — see <see cref="NotificationsTestPrincipals.NoEmailUserId"/>'s remarks).
/// </summary>
public class EmailMaskingMemberStandingAppService : IMemberStandingAppService
{
    private readonly IMemberStandingAppService _inner;

    public EmailMaskingMemberStandingAppService(IMemberStandingAppService inner)
    {
        _inner = inner;
    }

    public async Task<MemberStandingDto> GetByIdentityUserIdAsync(System.Guid identityUserId)
        => Mask(await _inner.GetByIdentityUserIdAsync(identityUserId));

    public async Task<MemberStandingDto> GetAsync(System.Guid memberId)
        => Mask(await _inner.GetAsync(memberId));

    public async Task<List<MemberStandingDto>> GetByIdsAsync(IEnumerable<System.Guid> memberIds)
    {
        var results = await _inner.GetByIdsAsync(memberIds);
        return results.Select(Mask).ToList();
    }

    private static MemberStandingDto Mask(MemberStandingDto dto)
    {
        if (dto.IdentityUserId == NotificationsTestPrincipals.NoEmailUserId)
        {
            dto.Email = null;
        }

        return dto;
    }
}

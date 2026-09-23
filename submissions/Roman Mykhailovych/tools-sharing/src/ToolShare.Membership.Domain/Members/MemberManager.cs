using System;
using System.Threading.Tasks;
using ToolShare.Membership.CommunityRules;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ToolShare.Membership.Members;

/// <summary>
/// The rules that need repository access and therefore cannot live on the
/// <see cref="Member"/> entity itself: MR-02's identity-uniqueness pre-check
/// (the unique index remains the authority under concurrency) and MR-10's
/// last-active-Administrator guard (FR-007, SC-006).
/// </summary>
public class MemberManager : DomainService
{
    private readonly IMemberRepository _memberRepository;

    public MemberManager(IMemberRepository memberRepository)
    {
        _memberRepository = memberRepository;
    }

    /// <summary>Enforces MR-02/MR-03.</summary>
    public async Task<Member> CreateAsync(Guid identityUserId, string displayName, string email, DateTime enrolledAt, Guid? enrolledByUserId)
    {
        if (await _memberRepository.FindByIdentityUserIdAsync(identityUserId) is not null)
        {
            throw new BusinessException(MembershipDomainErrorCodes.IdentityAlreadyEnrolled);
        }

        return new Member(GuidGenerator.Create(), identityUserId, displayName, email, enrolledAt, enrolledByUserId);
    }

    /// <summary>Enforces MR-10 before delegating to <see cref="Member.Deactivate"/>.</summary>
    public async Task DeactivateAsync(Member member, string reason, DateTime at, Guid? byUserId, CommunityRules.CommunityRules? communityRules = null)
    {
        await EnsureNotLastActiveAdministratorAsync(member);
        member.Deactivate(reason, at, byUserId, communityRules);
    }

    /// <summary>Enforces MR-10 before delegating to <see cref="Member.ChangeRole"/>.</summary>
    public async Task ChangeRoleAsync(Member member, CommunityRole newRole, DateTime at, Guid? byUserId, CommunityRules.CommunityRules? communityRules = null)
    {
        if (member.Role == CommunityRole.Administrator && newRole != CommunityRole.Administrator)
        {
            await EnsureNotLastActiveAdministratorAsync(member);
        }

        member.ChangeRole(newRole, at, byUserId, communityRules);
    }

    private async Task EnsureNotLastActiveAdministratorAsync(Member member)
    {
        if (member.Role != CommunityRole.Administrator || !member.IsActive)
        {
            return;
        }

        var otherActiveAdministrators = await _memberRepository.CountActiveAdministratorsAsync(excludedMemberId: member.Id);
        if (otherActiveAdministrators == 0)
        {
            throw new BusinessException(MembershipDomainErrorCodes.LastAdministrator);
        }
    }
}

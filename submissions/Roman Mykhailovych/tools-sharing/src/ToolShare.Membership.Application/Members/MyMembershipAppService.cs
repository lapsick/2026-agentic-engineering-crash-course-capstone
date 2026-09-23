using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Membership.CommunityRules;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ToolShare.Membership.Members;

/// <summary>
/// Self-service standing (US3). Resolves the caller's own <see cref="Member"/>
/// from <c>CurrentUser.Id</c> — never from a parameter — which is what makes
/// self-only access (SC-004) structural rather than a runtime check: see
/// <see cref="IMyMembershipAppService"/>'s remarks. Exposes no mutating
/// operation (US3 scenario 5).
/// <para>
/// The enrolment gate (<c>MembershipMethodInvocationAuthorizationService</c>)
/// already refuses any call from a caller who is not an enrolled, Active
/// member before this service runs, so <see cref="GetCurrentMemberOrThrowAsync"/>
/// only guards defensively against that invariant ever being violated.
/// </para>
/// </summary>
[Authorize]
public class MyMembershipAppService : ApplicationService, IMyMembershipAppService
{
    private readonly IMemberRepository _memberRepository;
    private readonly ICommunityRulesRepository _communityRulesRepository;

    public MyMembershipAppService(IMemberRepository memberRepository, ICommunityRulesRepository communityRulesRepository)
    {
        _memberRepository = memberRepository;
        _communityRulesRepository = communityRulesRepository;
    }

    public virtual async Task<MyMembershipDto> GetAsync()
    {
        var member = await GetCurrentMemberOrThrowAsync();
        var rules = await _communityRulesRepository.GetCurrentAsync();

        return new MyMembershipDto
        {
            MemberId = member.Id,
            DisplayName = member.DisplayName,
            Status = member.Status,
            Role = member.Role,
            CurrentRating = member.CurrentRating,
            EffectiveConcurrentLoanLimit = rules is null ? 0 : member.EffectiveConcurrentLoanLimit(rules),
            EnrolledAt = member.EnrolledAt
        };
    }

    /// <summary>
    /// Ordered <c>ChangedAt</c> ascending, tie-broken by <c>Id</c> (HR-03).
    /// Every <c>RatingOutcome</c> row carries both <c>EffectivePoints</c> and
    /// <c>ResultingRating</c>, so a member can account for the difference from
    /// 100 row by row (SC-012).
    /// </summary>
    public virtual async Task<List<MemberStandingChangeDto>> GetStandingHistoryAsync()
    {
        var member = await GetCurrentMemberOrThrowAsync();

        var withHistory = await _memberRepository.GetWithHistoryAsync(member.Id)
            ?? throw new EntityNotFoundException(typeof(Member), member.Id);

        var actorDisplayNames = await MemberStandingChangeDtoFactory.ResolveActorDisplayNamesAsync(_memberRepository, withHistory.StandingHistory);

        return MemberStandingChangeDtoFactory.ToOrderedDtos(withHistory.StandingHistory, actorDisplayNames);
    }

    private async Task<Member> GetCurrentMemberOrThrowAsync()
    {
        if (CurrentUser.Id is not { } identityUserId)
        {
            throw new BusinessException(MembershipDomainErrorCodes.NotAnEnrolledMember);
        }

        return await _memberRepository.FindByIdentityUserIdAsync(identityUserId)
            ?? throw new BusinessException(MembershipDomainErrorCodes.NotAnEnrolledMember);
    }
}

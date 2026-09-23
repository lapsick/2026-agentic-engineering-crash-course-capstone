using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Membership.CommunityRules;
using Volo.Abp.Application.Services;

namespace ToolShare.Membership.Members;

/// <summary>
/// The public standing lookup (US5, Tier 1). <see cref="GetByIdentityUserIdAsync"/>
/// resolves the enrolled/active flags through the same cached read the
/// enrolment gate uses (<see cref="IMemberStandingProvider"/>), so the hot
/// per-request lookup Lending will make on every borrowing decision (FR-024)
/// skips the identity-keyed database query; the remaining fields come from a
/// single indexed lookup by member id. Never throws for absence — an unknown
/// identity or member id yields <see cref="MemberStandingDto.IsEnrolled"/> false
/// (FR-025).
/// </summary>
[Authorize]
public class MemberStandingAppService : ApplicationService, IMemberStandingAppService
{
    private readonly IMemberRepository _memberRepository;
    private readonly ICommunityRulesRepository _communityRulesRepository;
    private readonly IMemberStandingProvider _memberStandingProvider;

    public MemberStandingAppService(
        IMemberRepository memberRepository,
        ICommunityRulesRepository communityRulesRepository,
        IMemberStandingProvider memberStandingProvider)
    {
        _memberRepository = memberRepository;
        _communityRulesRepository = communityRulesRepository;
        _memberStandingProvider = memberStandingProvider;
    }

    public virtual async Task<MemberStandingDto> GetByIdentityUserIdAsync(Guid identityUserId)
    {
        var snapshot = await _memberStandingProvider.GetByIdentityUserIdAsync(identityUserId);
        if (snapshot is null)
        {
            return NotEnrolled();
        }

        // Built entirely from the cached snapshot — no member-by-id database
        // round trip on the warm path (FR-024's perf goal). The rules row is
        // a single, PK-indexed lookup on a table with exactly one row and
        // does not scale with request volume the way a per-member lookup
        // would, so it is read live rather than cached separately (CRR-03
        // permits, but does not require, caching it).
        var rules = await _communityRulesRepository.GetCurrentAsync();

        return new MemberStandingDto
        {
            IsEnrolled = true,
            MemberId = snapshot.MemberId,
            IdentityUserId = identityUserId,
            DisplayName = snapshot.DisplayName,
            IsActive = snapshot.IsActive,
            Status = snapshot.Status,
            Role = snapshot.Role,
            CurrentRating = snapshot.CurrentRating,
            EffectiveConcurrentLoanLimit = rules is null ? 0 : rules.ComputeEffectiveConcurrentLoanLimit(snapshot.CurrentRating)
        };
    }

    public virtual async Task<MemberStandingDto> GetAsync(Guid memberId)
    {
        var member = await _memberRepository.FindAsync(memberId);
        return member is null ? NotEnrolled() : await ToDtoAsync(member);
    }

    public virtual async Task<List<MemberStandingDto>> GetByIdsAsync(IEnumerable<Guid> memberIds)
    {
        var ids = memberIds.ToList();
        if (ids.Count == 0)
        {
            return new List<MemberStandingDto>();
        }

        var members = await _memberRepository.GetListAsync(m => ids.Contains(m.Id));
        var rules = await _communityRulesRepository.GetCurrentAsync();

        return members.Select(member => ToDto(member, rules)).ToList();
    }

    private async Task<MemberStandingDto> ToDtoAsync(Member member)
    {
        var rules = await _communityRulesRepository.GetCurrentAsync();
        return ToDto(member, rules);
    }

    private static MemberStandingDto ToDto(Member member, CommunityRules.CommunityRules? rules)
    {
        return new MemberStandingDto
        {
            IsEnrolled = true,
            MemberId = member.Id,
            IdentityUserId = member.IdentityUserId,
            DisplayName = member.DisplayName,
            Email = member.Email,
            IsActive = member.IsActive,
            Status = member.Status,
            Role = member.Role,
            CurrentRating = member.CurrentRating,
            EffectiveConcurrentLoanLimit = rules is null ? 0 : member.EffectiveConcurrentLoanLimit(rules)
        };
    }

    private static MemberStandingDto NotEnrolled() => new();
}

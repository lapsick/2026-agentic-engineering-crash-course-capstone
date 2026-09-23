using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Membership.Members;
using ToolShare.Membership.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ToolShare.Membership.CommunityRules;

/// <summary>
/// Rules administration (US2). <see cref="GetAsync"/> is available to any
/// active member (FR-013) — the enrolment gate and the class-level
/// <see cref="AuthorizeAttribute"/> are the only requirements.
/// <see cref="UpdateAsync"/> additionally requires
/// <see cref="MembershipPermissions.Rules.Edit"/>.
/// </summary>
[Authorize]
public class CommunityRulesAppService : ApplicationService, ICommunityRulesAppService
{
    private readonly ICommunityRulesRepository _communityRulesRepository;
    private readonly IMemberRepository _memberRepository;

    public CommunityRulesAppService(
        ICommunityRulesRepository communityRulesRepository,
        IMemberRepository memberRepository)
    {
        _communityRulesRepository = communityRulesRepository;
        _memberRepository = memberRepository;
    }

    public virtual async Task<CommunityRulesDetailDto> GetAsync()
    {
        var rules = await GetCurrentRulesOrThrowAsync();
        return await MapToDetailDtoAsync(rules);
    }

    /// <summary>
    /// Cross-field validation (CRR-01) is delegated to
    /// <see cref="CommunityRules.Update"/>; nothing here re-validates the
    /// per-field ranges data annotations already caught.
    /// </summary>
    [Authorize(MembershipPermissions.Rules.Edit)]
    public virtual async Task<CommunityRulesDetailDto> UpdateAsync(UpdateCommunityRulesDto input)
    {
        var rules = await GetCurrentRulesOrThrowAsync();
        rules.ConcurrencyStamp = input.ConcurrencyStamp;

        rules.Update(
            input.MaxLoanTermDays,
            input.ConcurrentLoanLimit,
            input.LowRatingThreshold,
            input.ReducedConcurrentLoanLimit,
            input.OverduePenaltyPoints,
            input.DamagePenaltyPoints,
            input.CleanReturnRewardPoints,
            input.WaitlistOfferWindowHours,
            input.ReminderLeadTimeDays);

        await _communityRulesRepository.UpdateAsync(rules, autoSave: true);

        return await MapToDetailDtoAsync(rules);
    }

    /// <summary>The seeder guarantees exactly one row exists (CRR-02) before any user-facing call can be authorized.</summary>
    private async Task<CommunityRules> GetCurrentRulesOrThrowAsync()
    {
        return await _communityRulesRepository.GetCurrentAsync()
            ?? throw new AbpException("The community rules row is missing; the data seeder has not run.");
    }

    /// <summary>
    /// Resolves <see cref="CommunityRulesDetailDto.LastChangedByDisplayName"/> from
    /// the roster (<see cref="IMemberRepository"/>), never from the identity
    /// store — Membership must not reference Identity. The actor is always a
    /// member, since only an Administrator can reach <see cref="UpdateAsync"/>.
    /// </summary>
    private async Task<CommunityRulesDetailDto> MapToDetailDtoAsync(CommunityRules rules)
    {
        string? lastChangedByDisplayName = null;
        if (rules.LastModifierId is Guid lastModifierId)
        {
            var editor = await _memberRepository.FindByIdentityUserIdAsync(lastModifierId);
            lastChangedByDisplayName = editor?.DisplayName;
        }

        return new CommunityRulesDetailDto
        {
            MaxLoanTermDays = rules.MaxLoanTermDays,
            ConcurrentLoanLimit = rules.ConcurrentLoanLimit,
            LowRatingThreshold = rules.LowRatingThreshold,
            ReducedConcurrentLoanLimit = rules.ReducedConcurrentLoanLimit,
            OverduePenaltyPoints = rules.OverduePenaltyPoints,
            DamagePenaltyPoints = rules.DamagePenaltyPoints,
            CleanReturnRewardPoints = rules.CleanReturnRewardPoints,
            WaitlistOfferWindowHours = rules.WaitlistOfferWindowHours,
            ReminderLeadTimeDays = rules.ReminderLeadTimeDays,
            LastChangedAt = rules.LastModificationTime,
            LastChangedByUserId = rules.LastModifierId,
            LastChangedByDisplayName = lastChangedByDisplayName,
            ConcurrencyStamp = rules.ConcurrencyStamp
        };
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ToolShare.Membership.CommunityRules;

/// <summary>Public, read-only rules lookup (US5, Tier 1) — any active member (FR-013).</summary>
[Authorize]
public class CommunityRulesLookupAppService : ApplicationService, ICommunityRulesLookupAppService
{
    private readonly ICommunityRulesRepository _communityRulesRepository;

    public CommunityRulesLookupAppService(ICommunityRulesRepository communityRulesRepository)
    {
        _communityRulesRepository = communityRulesRepository;
    }

    public virtual async Task<CommunityRulesDto> GetAsync()
    {
        var rules = await _communityRulesRepository.GetCurrentAsync()
            ?? throw new AbpException("The community rules row is missing; the data seeder has not run.");

        return new CommunityRulesDto
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
            LastChangedByUserId = rules.LastModifierId
        };
    }
}

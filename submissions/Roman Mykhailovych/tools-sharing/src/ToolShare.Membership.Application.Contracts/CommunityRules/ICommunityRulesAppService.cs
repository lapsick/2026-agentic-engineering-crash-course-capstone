using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ToolShare.Membership.CommunityRules;

/// <summary>
/// Rules administration (US2). Tier 2 — module-internal, consumed only by
/// <c>ToolShare.Membership.Blazor</c>. Permission requirements are documented
/// in contracts/membership-permissions.md: <see cref="GetAsync"/> is available
/// to any active member (FR-013), <see cref="UpdateAsync"/> requires
/// <c>Membership.Rules.Edit</c>.
/// </summary>
public interface ICommunityRulesAppService : IApplicationService
{
    Task<CommunityRulesDetailDto> GetAsync();

    Task<CommunityRulesDetailDto> UpdateAsync(UpdateCommunityRulesDto input);
}

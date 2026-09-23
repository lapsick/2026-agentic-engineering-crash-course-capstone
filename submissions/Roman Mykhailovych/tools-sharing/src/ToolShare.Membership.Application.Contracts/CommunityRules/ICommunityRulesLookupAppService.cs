using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ToolShare.Membership.CommunityRules;

/// <summary>
/// The public, read-only rules lookup (US5, Tier 1). Mutation lives on the
/// internal <see cref="ICommunityRulesAppService"/>, Administrator-only.
/// </summary>
public interface ICommunityRulesLookupAppService : IApplicationService
{
    /// <summary>Never <c>null</c> — the seeder guarantees the single rules row exists (CRR-02).</summary>
    Task<CommunityRulesDto> GetAsync();
}

using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Membership.CommunityRules;

public interface ICommunityRulesRepository : IRepository<CommunityRules, System.Guid>
{
    /// <summary>Returns the single rules row, or <c>null</c> before the seeder has run (CRR-02).</summary>
    Task<CommunityRules?> GetCurrentAsync(CancellationToken cancellationToken = default);
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Membership.Repositories;

public class EfCoreCommunityRulesRepository
    : EfCoreRepository<MembershipDbContext, CommunityRules.CommunityRules, Guid>, ICommunityRulesRepository
{
    public EfCoreCommunityRulesRepository(IDbContextProvider<MembershipDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<CommunityRules.CommunityRules?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable.FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }
}

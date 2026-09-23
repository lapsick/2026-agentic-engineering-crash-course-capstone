using Microsoft.EntityFrameworkCore;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Membership.EntityFrameworkCore;

[ConnectionStringName(MembershipDbProperties.ConnectionStringName)]
public interface IMembershipDbContext : IEfCoreDbContext
{
    DbSet<Member> Members { get; }

    DbSet<MemberStandingChange> MemberStandingChanges { get; }

    DbSet<CommunityRules.CommunityRules> CommunityRules { get; }
}

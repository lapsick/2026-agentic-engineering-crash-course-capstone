using System;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Membership.EntityFrameworkCore;
using ToolShare.Membership.Members;
using ToolShare.Membership.Repositories;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;

namespace ToolShare.Membership;

[DependsOn(
    typeof(MembershipDomainModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule)
    )]
public class MembershipEntityFrameworkCoreModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Npgsql maps `timestamp with time zone` columns and requires DateTime.Kind
        // to be Utc; ABP's IClock defaults to Unspecified/Local otherwise, which
        // fails on first write (e.g. Member's EnrolledAt/ChangedAt). Same fix
        // Catalog applies for its own DateTime columns.
        Configure<AbpClockOptions>(options =>
        {
            options.Kind = DateTimeKind.Utc;
        });

        context.Services.AddAbpDbContext<MembershipDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<Member, EfCoreMemberRepository>();
            options.AddRepository<CommunityRules.CommunityRules, EfCoreCommunityRulesRepository>();
        });
    }
}

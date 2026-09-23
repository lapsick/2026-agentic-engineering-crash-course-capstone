using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.EntityFrameworkCore;
using ToolShare.Membership.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.Threading;

namespace ToolShare.Membership;

/// <summary>
/// Deliberately does NOT depend on <c>ToolShareTestBaseModule</c>, which calls
/// <c>AddAlwaysAllowAuthorization()</c> for the host's own tests — and
/// deliberately does NOT call it itself either: Membership's own suite needs
/// real permission/authorization enforcement so the enrolment gate and roster
/// authorization tests (SC-003/SC-004/SC-005) mean something. Mirrors
/// <c>CatalogApplicationTestModule</c> in every other respect, including
/// leaving unit-of-work transactions enabled (multiple sequential, mutating
/// app-service calls in one test rely on a real transaction per call).
/// </summary>
[DependsOn(
    typeof(MembershipApplicationModule),
    typeof(MembershipEntityFrameworkCoreModule),
    typeof(ToolShareEntityFrameworkCoreModule),
    // IMemberIdentityProvisioner's implementation (IdentityMemberIdentityProvisioner,
    // T056) lives in the host's Application layer, not in Membership itself
    // (research R2) — needed for every enrolment in this suite.
    typeof(ToolShareApplicationModule),
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule)
    )]
public class MembershipApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ConfigurePostgreSql(context.Services);
    }

    private static void ConfigurePostgreSql(IServiceCollection services)
    {
        var connectionString = AsyncHelper.RunSync(() => MembershipApplicationTestFixture.Instance!.CreateDatabaseAsync());

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<MembershipDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString, o =>
                    o.MigrationsHistoryTable("__EFMigrationsHistory", MembershipDbProperties.DbSchema));
            });

            options.Configure<ToolShareDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString);
            });

            options.Configure<PermissionManagementDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString);
            });
        });
    }
}

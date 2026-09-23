using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Catalog;
using ToolShare.Catalog.EntityFrameworkCore;
using ToolShare.EntityFrameworkCore;
using ToolShare.Lending.EntityFrameworkCore;
using ToolShare.Membership;
using ToolShare.Membership.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.Threading;

namespace ToolShare.Lending;

/// <summary>
/// Deliberately does NOT depend on <c>ToolShareTestBaseModule</c> (which calls
/// <c>AddAlwaysAllowAuthorization()</c>) and does NOT call it itself —
/// Lending's own suite needs real permission/authorization enforcement.
/// Mirrors <c>CatalogApplicationTestModule</c>/<c>MembershipApplicationTestModule</c>,
/// extended to compose all three business modules at once (research R9): the
/// first test module needing Catalog's, Membership's, and Lending's
/// <c>EntityFrameworkCore</c> layers simultaneously.
/// </summary>
[DependsOn(
    typeof(LendingApplicationModule),
    typeof(LendingEntityFrameworkCoreModule),
    typeof(CatalogApplicationModule),
    typeof(CatalogEntityFrameworkCoreModule),
    typeof(MembershipApplicationModule),
    typeof(MembershipEntityFrameworkCoreModule),
    typeof(ToolShareEntityFrameworkCoreModule),
    // IMemberIdentityProvisioner's implementation (IdentityMemberIdentityProvisioner)
    // lives in the host's Application layer, not in Membership itself (research R2) —
    // needed transitively by IMemberAppService, which US4's tests call directly
    // (adjusting a member's rating to prove eligibility checks are live).
    typeof(ToolShareApplicationModule),
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule)
    )]
public class LendingApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ConfigurePostgreSql(context.Services);
    }

    private static void ConfigurePostgreSql(IServiceCollection services)
    {
        var connectionString = AsyncHelper.RunSync(() => LendingApplicationTestFixture.Instance!.CreateDatabaseAsync());

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<LendingDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString, o =>
                    o.MigrationsHistoryTable("__EFMigrationsHistory", LendingDbProperties.DbSchema));
            });

            options.Configure<CatalogDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString, o =>
                    o.MigrationsHistoryTable("__EFMigrationsHistory", CatalogDbProperties.DbSchema));
            });

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

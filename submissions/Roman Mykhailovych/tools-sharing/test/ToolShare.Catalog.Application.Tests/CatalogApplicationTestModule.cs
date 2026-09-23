using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Catalog.EntityFrameworkCore;
using ToolShare.EntityFrameworkCore;
using ToolShare.Membership;
using ToolShare.Membership.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.Threading;

namespace ToolShare.Catalog;

/// <summary>
/// Deliberately does NOT depend on <c>ToolShareTestBaseModule</c>, which calls
/// <c>AddAlwaysAllowAuthorization()</c> for the host's own tests — Catalog's
/// own suite needs real permission enforcement so US3's authorization tests
/// (FR-011/FR-012, SC-003) mean something.
///
/// Deliberately does NOT call <c>AddAlwaysDisableUnitOfWorkTransaction()</c>
/// (unlike the host's read-mostly EF Core tests): this suite makes multiple
/// sequential, mutating app-service calls against the same aggregate, and
/// relies on each call's unit of work being a real database transaction so a
/// failed SaveChanges (e.g. a stale concurrency stamp) rolls back cleanly
/// instead of leaving a partially-committed batch behind.
///
/// Depends on <c>ToolShareEntityFrameworkCoreModule</c> (the host's own EF Core
/// module) purely for real, database-backed permission checking (Phase 5/US3):
/// <c>[Authorize(CatalogPermissions.*)]</c> ultimately resolves grants through
/// <c>PermissionManagementDbContext</c> (module-owned, not replaced onto
/// <c>ToolShareDbContext</c>), and role/user lookups go through
/// <c>ToolShareDbContext</c> itself (which replaces <c>IIdentityDbContext</c>).
/// Both point at the same per-test cloned database as <c>CatalogDbContext</c>,
/// mirroring the single physical "toolshare" database used in production.
/// </summary>
[DependsOn(
    typeof(CatalogApplicationModule),
    typeof(CatalogEntityFrameworkCoreModule),
    typeof(ToolShareEntityFrameworkCoreModule),
    // 003 ripple (research R10, T063): brings in the enrolment gate
    // (MembershipMethodInvocationAuthorizationService, registered by
    // MembershipApplicationModule) so Catalog's own suite is provably subject
    // to it too, plus IMemberRepository/MemberManager for seeding member
    // records for Catalog's synthetic test principals.
    typeof(MembershipApplicationModule),
    typeof(MembershipEntityFrameworkCoreModule),
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule)
    )]
public class CatalogApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ConfigurePostgreSql(context.Services);
    }

    private static void ConfigurePostgreSql(IServiceCollection services)
    {
        var connectionString = AsyncHelper.RunSync(() => CatalogApplicationTestFixture.Instance!.CreateDatabaseAsync());

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<CatalogDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString, o =>
                    o.MigrationsHistoryTable("__EFMigrationsHistory", CatalogDbProperties.DbSchema));
            });

            options.Configure<ToolShareDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString);
            });

            options.Configure<PermissionManagementDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString);
            });

            options.Configure<MembershipDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString, o =>
                    o.MigrationsHistoryTable("__EFMigrationsHistory", MembershipDbProperties.DbSchema));
            });
        });
    }
}

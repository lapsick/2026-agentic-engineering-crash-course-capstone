using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ToolShare.Catalog;
using ToolShare.Catalog.EntityFrameworkCore;
using ToolShare.EntityFrameworkCore;
using ToolShare.Membership;
using ToolShare.Membership.EntityFrameworkCore;
using ToolShare.Membership.Members;
using ToolShare.Notifications.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.Emailing;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.Threading;

namespace ToolShare.Notifications;

/// <summary>
/// Deliberately does NOT depend on <c>ToolShareTestBaseModule</c> and does NOT
/// call <c>AddAlwaysAllowAuthorization()</c> — Notifications' own suite needs
/// real permission/authorization enforcement (FR-014, `Notifications.Audit`).
/// Mirrors <c>LendingApplicationTestModule</c>, extended with a fake
/// <see cref="IEmailSender"/> (research R2) so no test ever attempts a real
/// network call.
/// </summary>
[DependsOn(
    typeof(NotificationsApplicationModule),
    typeof(NotificationsEntityFrameworkCoreModule),
    typeof(CatalogApplicationModule),
    typeof(CatalogEntityFrameworkCoreModule),
    typeof(MembershipApplicationModule),
    typeof(MembershipEntityFrameworkCoreModule),
    typeof(ToolShareEntityFrameworkCoreModule),
    typeof(ToolShareApplicationModule),
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule)
    )]
public class NotificationsApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ConfigurePostgreSql(context.Services);

        context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender>(sp => sp.GetRequiredService<FakeEmailSender>()));

        // FR-009's "no email on file" case (US2) — wraps the real
        // MemberStandingAppService so NoEmailUserId's email is stripped only
        // for callers going through this interface, without touching
        // Membership's own real invariant (every enrolled member has an
        // email) or its production registration.
        context.Services.Replace(ServiceDescriptor.Transient<IMemberStandingAppService>(sp =>
            new EmailMaskingMemberStandingAppService(ActivatorUtilities.CreateInstance<MemberStandingAppService>(sp))));
    }

    private static void ConfigurePostgreSql(IServiceCollection services)
    {
        var connectionString = AsyncHelper.RunSync(() => NotificationsApplicationTestFixture.Instance!.CreateDatabaseAsync());

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<NotificationsDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString, o =>
                    o.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbProperties.DbSchema));
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

            // Volo.Abp.BackgroundJobs' store uses its own, unreplaced
            // DbContext (research R2 — the first Notifications test module to
            // call IBackgroundJobManager.EnqueueAsync at all).
            options.Configure<BackgroundJobsDbContext>(context =>
            {
                context.DbContextOptions.UseNpgsql(connectionString);
            });
        });
    }
}

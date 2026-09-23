using System;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Notifications.EntityFrameworkCore;
using ToolShare.Notifications.Notifications;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;

namespace ToolShare.Notifications;

[DependsOn(
    typeof(NotificationsDomainModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule)
    )]
public class NotificationsEntityFrameworkCoreModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Npgsql maps `timestamp with time zone` columns and requires DateTime.Kind
        // to be Utc; same fix Catalog/Membership/Lending apply for their own
        // DateTime columns (CreatedAt, OriginatingChangedAt, AttemptedAt, CompletedAt).
        Configure<AbpClockOptions>(options =>
        {
            options.Kind = DateTimeKind.Utc;
        });

        context.Services.AddAbpDbContext<NotificationsDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<Notification, EfCoreNotificationRepository>();
        });
    }
}

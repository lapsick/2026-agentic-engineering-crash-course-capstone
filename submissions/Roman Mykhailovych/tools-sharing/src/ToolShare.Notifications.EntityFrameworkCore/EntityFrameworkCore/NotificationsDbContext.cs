using Microsoft.EntityFrameworkCore;
using ToolShare.Notifications.Notifications;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Notifications.EntityFrameworkCore;

[ConnectionStringName(NotificationsDbProperties.ConnectionStringName)]
public class NotificationsDbContext : AbpDbContext<NotificationsDbContext>, INotificationsDbContext
{
    public DbSet<Notification> Notifications { get; set; } = null!;

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(NotificationsDbProperties.DbSchema);

        builder.ConfigureNotifications();
    }
}

using Microsoft.EntityFrameworkCore;
using ToolShare.Notifications.Notifications;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace ToolShare.Notifications.EntityFrameworkCore;

[ConnectionStringName(NotificationsDbProperties.ConnectionStringName)]
public interface INotificationsDbContext : IEfCoreDbContext
{
    DbSet<Notification> Notifications { get; }
}

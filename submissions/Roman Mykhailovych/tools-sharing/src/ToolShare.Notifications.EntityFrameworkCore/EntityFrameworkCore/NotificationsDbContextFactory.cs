using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ToolShare.Notifications.EntityFrameworkCore;

/* Needed for EF Core console commands (dotnet ef migrations add / database update). */
public class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Default"), o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbProperties.DbSchema));

        return new NotificationsDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ToolShare.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}

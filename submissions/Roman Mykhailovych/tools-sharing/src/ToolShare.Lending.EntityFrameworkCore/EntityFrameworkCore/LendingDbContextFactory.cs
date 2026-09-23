using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ToolShare.Lending.EntityFrameworkCore;

/* Needed for EF Core console commands (dotnet ef migrations add / database update). */
public class LendingDbContextFactory : IDesignTimeDbContextFactory<LendingDbContext>
{
    public LendingDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<LendingDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Default"), o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", LendingDbProperties.DbSchema));

        return new LendingDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ToolShare.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}

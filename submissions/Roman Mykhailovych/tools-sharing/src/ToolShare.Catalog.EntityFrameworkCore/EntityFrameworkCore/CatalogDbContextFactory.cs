using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ToolShare.Catalog.EntityFrameworkCore;

/* Needed for EF Core console commands (dotnet ef migrations add / database update). */
public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Default"), o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", CatalogDbProperties.DbSchema));

        return new CatalogDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ToolShare.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}

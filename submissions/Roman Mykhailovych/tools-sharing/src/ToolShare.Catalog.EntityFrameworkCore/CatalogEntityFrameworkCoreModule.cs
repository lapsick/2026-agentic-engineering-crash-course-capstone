using System;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.EntityFrameworkCore;
using ToolShare.Catalog.Repositories;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Catalog.Tools;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;

namespace ToolShare.Catalog;

[DependsOn(
    typeof(CatalogDomainModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule)
    )]
public class CatalogEntityFrameworkCoreModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Npgsql maps `timestamp with time zone` columns and requires DateTime.Kind
        // to be Utc; ABP's IClock defaults to Unspecified/Local otherwise, which
        // fails on first write (e.g. ToolInstanceManager.CreateAsync's registeredAt).
        Configure<AbpClockOptions>(options =>
        {
            options.Kind = DateTimeKind.Utc;
        });

        context.Services.AddAbpDbContext<CatalogDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<Category, EfCoreCategoryRepository>();
            options.AddRepository<Tool, EfCoreToolRepository>();
            options.AddRepository<ToolInstance, EfCoreToolInstanceRepository>();
        });
    }
}

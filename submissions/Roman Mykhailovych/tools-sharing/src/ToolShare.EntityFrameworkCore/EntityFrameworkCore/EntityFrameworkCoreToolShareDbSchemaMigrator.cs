using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Data;
using Volo.Abp.DependencyInjection;

namespace ToolShare.EntityFrameworkCore;

public class EntityFrameworkCoreToolShareDbSchemaMigrator
    : IToolShareDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreToolShareDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolve the ToolShareDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<ToolShareDbContext>()
            .Database
            .MigrateAsync();
    }
}

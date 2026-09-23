using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToolShare.Data;
using ToolShare.Membership;
using Serilog;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Identity;

namespace ToolShare.DbMigrator;

public class DbMigratorHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IConfiguration _configuration;

    public DbMigratorHostedService(IHostApplicationLifetime hostApplicationLifetime, IConfiguration configuration)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using (var application = await AbpApplicationFactory.CreateAsync<ToolShareDbMigratorModule>(options =>
        {
           options.Services.ReplaceConfiguration(_configuration);
           options.UseAutofac();
           options.Services.AddLogging(c => c.AddSerilog());
           options.AddDataMigrationEnvironment();
        }))
        {
            await application.InitializeAsync();

            // ToolShareDbContext now owns Catalog's and Membership's schemas too
            // (ReplaceDbContext/consolidated migrations — see ToolShareDbContext),
            // so a single MigrateAsync() call below creates every schema before
            // the seed step runs; no separate per-module migrator pass needed.
            await application
                .ServiceProvider
                .GetRequiredService<ToolShareDbMigrationService>()
                .MigrateAsync();

            // A second seed pass, now that IdentityDataSeedContributor has
            // (idempotently) created the "admin" identity user above:
            // MembershipDataSeedContributor needs the admin's real identity user
            // id to create their bootstrap Member record (FR-010), but
            // Membership.Domain must not depend on the Identity module (research
            // R2) and cannot look this up itself. The DbMigrator — composed here,
            // not the host's own Domain/Application layers — is the only project
            // allowed to know about every module (R11 precedent), so this
            // orchestration lives here rather than in ToolShareDbMigrationService.
            // Both passes are idempotent, so running the whole pipeline twice is
            // safe.
            var adminUser = await application.ServiceProvider
                .GetRequiredService<IIdentityUserRepository>()
                .FindByNormalizedUserNameAsync(IdentityDataSeedContributor.AdminUserNameDefaultValue.ToUpperInvariant());

            if (adminUser != null)
            {
                await application.ServiceProvider
                    .GetRequiredService<IDataSeeder>()
                    .SeedAsync(new DataSeedContext()
                        .WithProperty(MembershipDataSeedContributor.AdminIdentityUserIdPropertyName, adminUser.Id)
                        .WithProperty(MembershipDataSeedContributor.AdminDisplayNamePropertyName, adminUser.Name)
                        .WithProperty(MembershipDataSeedContributor.AdminEmailPropertyName, adminUser.Email));
            }

            await application.ShutdownAsync();

            _hostApplicationLifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

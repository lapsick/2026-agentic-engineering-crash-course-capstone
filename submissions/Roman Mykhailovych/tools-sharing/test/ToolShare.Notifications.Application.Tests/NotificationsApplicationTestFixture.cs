using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.EntityFrameworkCore;
using ToolShare.Membership;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Xunit;

namespace ToolShare.Notifications;

/// <summary>
/// Real PostgreSQL for Notifications' application-layer integration tests
/// (Constitution V). Mirrors <c>LendingApplicationTestFixture</c> exactly — the
/// second test fixture composing Catalog, Membership, and (here) Notifications'
/// schema all at once, since `ToolShareDbContext` now owns all four.
/// </summary>
public class NotificationsApplicationTestFixture : IAsyncLifetime
{
    public static NotificationsApplicationTestFixture? Instance { get; private set; }

    private readonly PostgreSqlContainerFixture _postgreSqlContainerFixture = new();

    public async Task InitializeAsync()
    {
        await _postgreSqlContainerFixture.InitializeAsync();

        await _postgreSqlContainerFixture.EnsureTemplateMigratedAsync(async connectionString =>
        {
            await MigrateAndSeedHostSchemaAsync(connectionString);
        });

        Instance = this;
    }

    private static async Task MigrateAndSeedHostSchemaAsync(string connectionString)
    {
        var hostOptions = new DbContextOptionsBuilder<ToolShareDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var hostContext = new ToolShareDbContext(hostOptions))
        {
            await hostContext.Database.MigrateAsync();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString
            })
            .Build();

        using var application = await AbpApplicationFactory.CreateAsync<NotificationsAuthorizationSeedModule>(options =>
        {
            options.UseAutofac();
            options.Services.ReplaceConfiguration(configuration);
        });

        await application.InitializeAsync();

        await application.ServiceProvider
            .GetRequiredService<IDataSeeder>()
            .SeedAsync(new DataSeedContext()
                .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, IdentityDataSeedContributor.AdminEmailDefaultValue)
                .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, IdentityDataSeedContributor.AdminPasswordDefaultValue));

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
    }

    /// <summary>A fresh database cloned from the migrated template, for one test.</summary>
    public Task<string> CreateDatabaseAsync()
    {
        return _postgreSqlContainerFixture.CreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgreSqlContainerFixture.DisposeAsync();
        Instance = null;
    }
}

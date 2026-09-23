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

namespace ToolShare.Lending;

/// <summary>
/// Real PostgreSQL for Lending's application-layer integration tests
/// (Constitution V). The first test fixture composing all three business
/// modules at once (research R9): <c>ToolShareDbContext</c> now owns Catalog's,
/// Membership's, and Lending's schemas too (`[ReplaceDbContext]`/consolidated
/// migrations), so migrating it alone creates every schema before the seed
/// step runs — mirrors <c>CatalogApplicationTestFixture</c>/
/// <c>MembershipApplicationTestFixture</c> exactly.
/// </summary>
public class LendingApplicationTestFixture : IAsyncLifetime
{
    public static LendingApplicationTestFixture? Instance { get; private set; }

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

    /// <summary>
    /// Migrates the consolidated host schema (Identity, PermissionManagement,
    /// Catalog, Membership, Lending, ...) and runs the same seed pipeline
    /// <c>ToolShare.DbMigrator</c> runs in production (admin user/role with
    /// every permission, plus the Librarian/Administrator role seeders) against
    /// the template database, then Lending's own test-principal member records.
    /// </summary>
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

        using var application = await AbpApplicationFactory.CreateAsync<LendingAuthorizationSeedModule>(options =>
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

        // Second pass, now that the "admin" identity user exists: gives the
        // bootstrap Administrator a real Member record (FR-010), mirroring
        // DbMigratorHostedService in production.
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

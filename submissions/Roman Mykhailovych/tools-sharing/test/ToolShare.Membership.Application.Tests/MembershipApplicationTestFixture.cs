using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.EntityFrameworkCore;
using ToolShare.Membership.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Xunit;

namespace ToolShare.Membership;

/// <summary>
/// Real PostgreSQL for Membership's application-layer integration tests
/// (Constitution V). Mirrors <c>CatalogApplicationTestFixture</c>: one
/// Testcontainers PostgreSQL instance per test assembly, a template database
/// migrated once, and every test class cloning its own database from that
/// template (<see cref="CreateDatabaseAsync"/>).
/// </summary>
public class MembershipApplicationTestFixture : IAsyncLifetime
{
    public static MembershipApplicationTestFixture? Instance { get; private set; }

    private readonly PostgreSqlContainerFixture _postgreSqlContainerFixture = new();

    public async Task InitializeAsync()
    {
        await _postgreSqlContainerFixture.InitializeAsync();

        await _postgreSqlContainerFixture.EnsureTemplateMigratedAsync(async connectionString =>
        {
            var options = new DbContextOptionsBuilder<MembershipDbContext>()
                .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", MembershipDbProperties.DbSchema))
                .Options;

            await using var context = new MembershipDbContext(options);
            await context.Database.MigrateAsync();

            await MigrateAndSeedHostSchemaAsync(connectionString);
        });

        Instance = this;
    }

    /// <summary>
    /// Migrates the host's own schema (Identity, PermissionManagement, ...) and
    /// runs the same seed pipeline <c>ToolShare.DbMigrator</c> runs in production
    /// (admin user/role with every permission) against the template database, so
    /// every per-test clone starts with a real, permission-checkable admin
    /// already in place — mirrors <c>CatalogApplicationTestFixture</c>.
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

        using var application = await AbpApplicationFactory.CreateAsync<MembershipAuthorizationSeedModule>(options =>
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
        // bootstrap Administrator a real Member record (FR-010) so tests that
        // impersonate the real seeded admin (mirroring
        // CatalogAuthorizationTestBase.BuildAdminPrincipalAsync) pass the
        // enrolment gate too. Mirrors the same two-pass approach
        // DbMigratorHostedService uses in production.
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

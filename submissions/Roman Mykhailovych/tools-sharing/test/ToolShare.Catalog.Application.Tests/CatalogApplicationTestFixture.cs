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

namespace ToolShare.Catalog;

/// <summary>
/// Real PostgreSQL for Catalog's application-layer integration tests
/// (Constitution V). Unlike the host's read-only sample tests, these tests
/// mutate data extensively, so every test gets its own database cloned from
/// the migrated template (<see cref="CreateDatabaseAsync"/>) rather than
/// sharing one connection for the whole collection.
/// </summary>
public class CatalogApplicationTestFixture : IAsyncLifetime
{
    public static CatalogApplicationTestFixture? Instance { get; private set; }

    private readonly PostgreSqlContainerFixture _postgreSqlContainerFixture = new();

    public async Task InitializeAsync()
    {
        await _postgreSqlContainerFixture.InitializeAsync();

        await _postgreSqlContainerFixture.EnsureTemplateMigratedAsync(async connectionString =>
        {
            // ToolShareDbContext now owns Catalog's and Membership's schemas too
            // (ReplaceDbContext/consolidated migrations — see ToolShareDbContext),
            // so migrating it alone creates every schema before the seed step runs.
            await MigrateAndSeedHostSchemaAsync(connectionString);
        });

        Instance = this;
    }

    /// <summary>
    /// Migrates the host's own schema (Identity, PermissionManagement, ...) and
    /// runs the same seed pipeline <c>ToolShare.DbMigrator</c> runs in production
    /// (admin user/role with every permission, plus <c>LibrarianRoleDataSeedContributor</c>)
    /// against the template database, so every per-test clone starts with a real,
    /// permission-checkable admin and Librarian role already in place (Phase 5/US3).
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

        using var application = await AbpApplicationFactory.CreateAsync<CatalogAuthorizationSeedModule>(options =>
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

        // Second pass (research R10, T063), mirroring DbMigratorHostedService in
        // production: gives the seeded "admin" a real bootstrap-Administrator
        // Member record so AdminBootstrapTests' real-admin impersonation
        // (BuildAdminPrincipalAsync) survives the enrolment gate.
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

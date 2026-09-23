using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ToolShare.EntityFrameworkCore;

/// <summary>
/// Real PostgreSQL for the host's EF Core integration tests (Constitution V —
/// no SQLite/in-memory substitute). One container for the whole collection;
/// the template database is migrated once and shared by every test class in
/// this collection (they are read-only against seeded data, so no per-class
/// database clone is needed here — contrast with Catalog's application tests,
/// which do mutate data and clone per class).
/// </summary>
public class ToolShareEntityFrameworkCoreFixture : IAsyncLifetime
{
    public static ToolShareEntityFrameworkCoreFixture? Instance { get; private set; }

    private readonly PostgreSqlContainerFixture _postgreSqlContainerFixture = new();

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgreSqlContainerFixture.InitializeAsync();

        await _postgreSqlContainerFixture.EnsureTemplateMigratedAsync(async connectionString =>
        {
            var options = new DbContextOptionsBuilder<ToolShareDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            await using var context = new ToolShareDbContext(options);
            await context.Database.MigrateAsync();
        });

        ConnectionString = _postgreSqlContainerFixture.TemplateConnectionString;
        Instance = this;
    }

    public async Task DisposeAsync()
    {
        await _postgreSqlContainerFixture.DisposeAsync();
        Instance = null;
    }
}

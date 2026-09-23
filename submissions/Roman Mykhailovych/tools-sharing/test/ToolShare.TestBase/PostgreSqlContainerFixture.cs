using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ToolShare;

/// <summary>
/// One PostgreSQL 16 container per test assembly (Constitution V — no
/// SQLite/in-memory substitute anywhere). A single "template" database is
/// migrated once per assembly; each test class then gets its own database
/// cloned from that template via <c>CREATE DATABASE ... TEMPLATE ...</c>,
/// which is a fast file-level copy rather than a fresh container or a full
/// re-migration. Shared by the host and every Catalog-style module's test
/// project so the Testcontainers setup is written exactly once.
/// </summary>
public class PostgreSqlContainerFixture : IAsyncLifetime
{
    private const string TemplateDatabaseName = "toolshare_template";

    private readonly SemaphoreSlim _templateMigrationLock = new(1, 1);
    private bool _templateMigrated;

    private PostgreSqlContainer _container = null!;

    /// <summary>Connection string for the migrated template database.</summary>
    public string TemplateConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase(TemplateDatabaseName)
            // Every module's test suite clones a fresh database per test, each with its
            // own Npgsql connection pool; idle pooled connections from earlier tests
            // aren't proactively closed, so a full run can otherwise exceed Postgres's
            // default max_connections (100) well before any test is actually using
            // that many connections concurrently.
            .WithCommand("-c", "max_connections=300")
            .Build();

        await _container.StartAsync();

        TemplateConnectionString = _container.GetConnectionString();
    }

    /// <summary>
    /// Runs <paramref name="migrateTemplateAsync"/> against the template database
    /// exactly once per fixture instance, regardless of how many test classes call this.
    /// </summary>
    public async Task EnsureTemplateMigratedAsync(Func<string, Task> migrateTemplateAsync)
    {
        if (_templateMigrated)
        {
            return;
        }

        await _templateMigrationLock.WaitAsync();
        try
        {
            if (_templateMigrated)
            {
                return;
            }

            await migrateTemplateAsync(TemplateConnectionString);

            // CREATE DATABASE ... TEMPLATE fails if the template still has any
            // pooled connections open — Npgsql pools by default and disposing
            // a DbContext only returns the connection to the pool, it doesn't
            // close it. Force every pooled connection closed before anyone clones.
            NpgsqlConnection.ClearAllPools();

            _templateMigrated = true;
        }
        finally
        {
            _templateMigrationLock.Release();
        }
    }

    /// <summary>Clones a fresh database from the migrated template for one test class.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var databaseName = $"test_{Guid.NewGuid():N}";

        var maintenanceConnectionStringBuilder = new NpgsqlConnectionStringBuilder(TemplateConnectionString)
        {
            Database = "postgres"
        };

        await using (var connection = new NpgsqlConnection(maintenanceConnectionStringBuilder.ConnectionString))
        {
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\" TEMPLATE \"{TemplateDatabaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        var databaseConnectionStringBuilder = new NpgsqlConnectionStringBuilder(TemplateConnectionString)
        {
            Database = databaseName
        };

        return databaseConnectionStringBuilder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

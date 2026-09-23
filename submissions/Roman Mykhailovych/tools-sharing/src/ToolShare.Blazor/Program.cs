using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Serilog;
using Serilog.Events;

namespace ToolShare.Blazor;

public class Program
{
    public async static Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Warning()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateLogger();

        try
        {
            Log.Information("Starting web host.");
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.AddAppSettingsSecretsJson()
                .UseAutofac()
                .UseSerilog();
            await builder.AddApplicationAsync<ToolShareBlazorModule>();
            var app = builder.Build();

            // FR-020: refuse to start (and never open the port / serve a
            // single page) if the configured database is unreachable —
            // otherwise ASP.NET Core happily starts listening and only the
            // first request that happens to touch a DbContext fails, which
            // looks like an empty/broken page rather than a clear startup
            // error. Deliberately checked here, before
            // InitializeApplicationAsync/RunAsync, so nothing is served.
            await EnsureDatabaseIsReachableAsync(app.Configuration);

            await app.InitializeApplicationAsync();
            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            if (ex is HostAbortedException)
            {
                throw;
            }

            Log.Fatal(ex, "Host terminated unexpectedly!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task EnsureDatabaseIsReachableAsync(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured - refusing to start (FR-020).");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(
                ex,
                "Cannot reach the database with the configured 'Default' connection string - refusing to start (FR-020). Fix connectivity/credentials and restart.");
            throw;
        }
    }
}

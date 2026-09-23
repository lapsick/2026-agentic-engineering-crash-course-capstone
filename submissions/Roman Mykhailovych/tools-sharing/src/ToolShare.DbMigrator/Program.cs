using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace ToolShare.DbMigrator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
#if DEBUG
                .MinimumLevel.Override("ToolShare", LogEventLevel.Debug)
#else
                .MinimumLevel.Override("ToolShare", LogEventLevel.Information)
#endif
                .Enrich.FromLogContext()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateLogger();

        try
        {
            // FR-020: an unreachable database (e.g. wrong connection string,
            // Postgres not up yet) must fail this step fast and loudly with
            // the underlying Npgsql error — DbMigratorHostedService.StartAsync
            // lets that exception propagate uncaught, so RunConsoleAsync
            // throws here; logging it via the already-configured Serilog
            // sinks (Console + file) before returning a non-zero exit code
            // is what actually surfaces it to `docker compose logs`/CI,
            // rather than a silent/ambiguous process exit.
            await CreateHostBuilder(args).RunConsoleAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "ToolShare.DbMigrator terminated unexpectedly - migration did not complete.");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .AddAppSettingsSecretsJson()
            .ConfigureLogging((context, logging) => logging.ClearProviders())
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<DbMigratorHostedService>();
            });
}

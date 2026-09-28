using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace ToolShare.E2E;

public static class E2EConsts
{
    public const string CollectionDefinitionName = "E2ECollection";
}

[CollectionDefinition(E2EConsts.CollectionDefinitionName)]
public class E2ECollection : ICollectionFixture<E2EAppFixture>
{
}

/// <summary>
/// The whole application, the way a user meets it: a real PostgreSQL 16 container
/// (Constitution V), migrated and seeded by <c>ToolShare.DbMigrator</c> exactly as in
/// production (Constitution VI — the app itself never migrates), then <c>ToolShare.Blazor</c>
/// started with <c>dotnet run</c> on a free local port, and one headless Chromium shared by
/// the collection. Started once per test assembly; each test gets its own browser context.
/// </summary>
public class E2EAppFixture : IAsyncLifetime
{
    /// <summary>The bootstrap administrator seeded by DbMigrator (ABP's IdentityDataSeedContributor defaults).</summary>
    public const string AdminUserName = "admin";

    public static readonly string AdminPassword =
        Environment.GetEnvironmentVariable("TOOLSHARE_E2E_ADMIN_PASSWORD") ?? "1q2w3E*";

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private readonly PostgreSqlContainerFixture _postgreSql = new();
    private readonly StringBuilder _appOutput = new();
    private Process? _app;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // No-op when Chromium is already installed; downloads it on the first run.
        var installExitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (installExitCode != 0)
        {
            throw new InvalidOperationException($"Playwright could not install Chromium (exit code {installExitCode}).");
        }

        await _postgreSql.InitializeAsync();
        var connectionString = _postgreSql.TemplateConnectionString;
        var srcDir = Path.Combine(FindRepositoryRoot(), "src");

        var migrator = StartDotnetRun(Path.Combine(srcDir, "ToolShare.DbMigrator"), new Dictionary<string, string>
        {
            ["ConnectionStrings__Default"] = connectionString
        });
        if (!migrator.WaitForExit((int)StartupTimeout.TotalMilliseconds) || migrator.ExitCode != 0)
        {
            migrator.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"ToolShare.DbMigrator failed (exit {(migrator.HasExited ? migrator.ExitCode : -1)}).\n{AppOutputTail()}");
        }

        BaseUrl = $"http://127.0.0.1:{GetFreePort()}";
        _app = StartDotnetRun(Path.Combine(srcDir, "ToolShare.Blazor"), new Dictionary<string, string>
        {
            // Development: serves the modules' static web assets straight from the build output.
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = BaseUrl,
            ["ConnectionStrings__Default"] = connectionString,
            ["App__SelfUrl"] = BaseUrl,
            ["App__RedirectAllowedUrls"] = BaseUrl,
            ["AuthServer__Authority"] = BaseUrl,
            ["AuthServer__RequireHttpsMetadata"] = "false"
        });
        await WaitUntilServingAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Environment.GetEnvironmentVariable("TOOLSHARE_E2E_HEADED") != "1"
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_app is { HasExited: false })
        {
            _app.Kill(entireProcessTree: true);
        }

        await _postgreSql.DisposeAsync();
    }

    /// <summary>The last lines the migrator/app wrote — attached to failures so a red run explains itself.</summary>
    public string AppOutputTail(int lines = 60)
    {
        lock (_appOutput)
        {
            return string.Join('\n', _appOutput.ToString().Split('\n').TakeLast(lines));
        }
    }

    private Process StartDotnetRun(string projectDir, IDictionary<string, string> environment)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            // Run from the project's own directory so its appsettings.json resolves.
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in new[] { "run", "--no-launch-profile" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => AppendOutput(e.Data);
        process.ErrorDataReceived += (_, e) => AppendOutput(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private void AppendOutput(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_appOutput)
        {
            _appOutput.AppendLine(line);
        }
    }

    private async Task WaitUntilServingAsync()
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow + StartupTimeout;

        while (DateTime.UtcNow < deadline)
        {
            if (_app!.HasExited)
            {
                throw new InvalidOperationException($"ToolShare.Blazor exited during startup (exit {_app.ExitCode}).\n{AppOutputTail()}");
            }

            try
            {
                var response = await http.GetAsync(BaseUrl + "/");
                if ((int)response.StatusCode < 500)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not listening yet (still building/starting).
            }
            catch (TaskCanceledException)
            {
                // Request timed out while the app warms up.
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException($"ToolShare.Blazor did not start serving {BaseUrl} within {StartupTimeout}.\n{AppOutputTail()}");
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ToolShare.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (ToolShare.slnx) from " + AppContext.BaseDirectory);
    }
}

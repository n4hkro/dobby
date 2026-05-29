using Dobby.Configuration;
using Dobby.Host;
using Dobby.Host.Startup;
using Serilog;
using Shouldly;

namespace Dobby.IntegrationTests.Diagnostics;

public sealed class LocalDiagnosticsSinkTests
{
    [Fact]
    public void Local_diagnostics_sink_writes_under_runtime_logs_and_redacts_at_write_time()
    {
        using var temp = new TemporaryDirectory();
        using (var logger = LocalDiagnosticsConfigurator.CreateLogger(
            new RuntimeOptions { DataRoot = temp.Path },
            new DiagnosticsOptions(),
            "Information"))
        {
            logger.Information("password=secret token=abc safe=şəffaf");
        }

        var logFile = Directory.EnumerateFiles(Path.Combine(temp.Path, "logs"), "*.log").Single();
        var contents = File.ReadAllText(logFile);

        contents.ShouldContain("password=<redacted>");
        contents.ShouldContain("token=<redacted>");
        contents.ShouldContain("safe=şəffaf");
        contents.ShouldNotContain("secret");
        contents.ShouldNotContain("abc");
    }

    [Fact]
    public void Local_diagnostics_sink_writes_structured_boundary_fields()
    {
        using var temp = new TemporaryDirectory();
        using (var logger = LocalDiagnosticsConfigurator.CreateLogger(
            new RuntimeOptions { DataRoot = temp.Path },
            new DiagnosticsOptions(),
            "Information"))
        {
            logger
                .ForContext("SourceContext", "Dobby.Host.Diagnostics")
                .ForContext("CorrelationId", "corr-123")
                .Information("operation {OperationId} completed", "op-456");
        }

        var logFile = Directory.EnumerateFiles(Path.Combine(temp.Path, "logs"), "*.log").Single();
        var contents = File.ReadAllText(logFile);

        contents.ShouldContain("timestamp=");
        contents.ShouldContain("level=Information");
        contents.ShouldContain("sourceContext=Dobby.Host.Diagnostics");
        contents.ShouldContain("correlationId=corr-123");
        contents.ShouldContain("operationId=op-456");
        contents.ShouldContain("messageTemplate=\"operation {OperationId} completed\"");
    }

    [Fact]
    public void Local_diagnostics_sink_enforces_total_size_cap_for_existing_logs()
    {
        using var temp = new TemporaryDirectory();
        var logsDirectory = Path.Combine(temp.Path, "logs");
        Directory.CreateDirectory(logsDirectory);
        for (var index = 0; index < 33; index++)
        {
            var path = Path.Combine(logsDirectory, $"old-{index:00}.log");
            using var stream = File.Create(path);
            stream.SetLength(1024 * 1024);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-index));
        }

        using (LocalDiagnosticsConfigurator.CreateLogger(
            new RuntimeOptions { DataRoot = temp.Path },
            new DiagnosticsOptions { TechnicalLogTotalSizeCapMegabytes = 32 },
            "Information"))
        {
        }

        var totalBytes = Directory
            .EnumerateFiles(logsDirectory, "*.log")
            .Sum(file => new FileInfo(file).Length);
        totalBytes.ShouldBeLessThanOrEqualTo(32L * 1024L * 1024L);
    }

    [Fact]
    public void Local_diagnostics_sink_uses_configured_minimum_level_without_rebuild()
    {
        using var temp = new TemporaryDirectory();
        using (var logger = LocalDiagnosticsConfigurator.CreateLogger(
            new RuntimeOptions { DataRoot = temp.Path },
            new DiagnosticsOptions(),
            "Warning"))
        {
            logger.Information("level=information token=abc");
            logger.Warning("level=warning password=secret");
        }

        var logFile = Directory.EnumerateFiles(Path.Combine(temp.Path, "logs"), "*.log").Single();
        var contents = File.ReadAllText(logFile);

        contents.ShouldContain("level=warning");
        contents.ShouldContain("password=<redacted>");
        contents.ShouldNotContain("level=information");
        contents.ShouldNotContain("token=abc");
        contents.ShouldNotContain("secret");
    }

    [Fact]
    public void Host_bootstrap_reads_serilog_minimum_level_from_top_level_configuration_section()
    {
        using var temp = new TemporaryDirectory();
        var runtimeRoot = Path.Combine(temp.Path, "runtime-root");
        var appsettings = Path.Combine(temp.Path, "appsettings.json");
        WriteBootstrapAppSettings(appsettings, runtimeRoot);

        HostBootstrapper.Bootstrap(new HostBootstrapOptions(
            ShowMainWindow: false,
            PackagedAppSettingsPath: appsettings,
            RuntimeConfigDirectory: Path.Combine(temp.Path, "config"),
            EnvironmentName: "Test"));

        var logContents = Directory
            .EnumerateFiles(Path.Combine(runtimeRoot, "logs"), "*.log")
            .Select(File.ReadAllText);
        string.Join(Environment.NewLine, logContents).ShouldNotContain("Runtime folder ready:");
    }

    private static void WriteBootstrapAppSettings(string appsettingsPath, string runtimeRoot)
    {
        File.WriteAllText(appsettingsPath, $$"""
            {
              "Dobby": {
                "Runtime": { "DataRoot": "{{runtimeRoot.Replace("\\", "\\\\")}}", "EnvironmentName": "Test" },
                "Storage": { "ProviderMode": "SQLite3MultipleCiphers", "RequireDataAtRestProtection": true, "BusyTimeoutMilliseconds": 5000 },
                "Auth": { "LoginThrottleStepsSeconds": [0, 0, 10, 30, 60], "PasswordKdfMode": "Pbkdf2Sha256" },
                "Session": { "AbsoluteTimeoutMinutes": 480, "IdleTimeoutMinutes": 30, "AutoLockEnabledByDefault": false },
                "Diagnostics": { "TechnicalLogRetentionDays": 30, "TechnicalLogTotalSizeCapMegabytes": 256 },
                "Audit": { "RetentionDays": 365 },
                "Localization": { "DefaultCulture": "az-AZ", "SupportedCultures": ["az-AZ", "en-US", "tr-TR"] },
                "Plugins": { "AllowPreLoginDiscovery": false },
                "Licensing": { "ImportRequiresPermission": true },
                "BranchShared": { "RuntimeEnabled": false }
              },
              "Serilog": { "MinimumLevel": { "Default": "Error" } }
            }
            """);
    }
}

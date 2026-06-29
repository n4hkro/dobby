using Dobby.Audit;
using Dobby.Diagnostics;
using Dobby.Host.Startup;
using Shouldly;
using System.IO;

namespace Dobby.IntegrationTests.Startup;

public sealed class StartupBootstrapWiringTests
{
    [Fact]
    public void App_xaml_does_not_bypass_bootstrap_with_startup_uri()
    {
        var root = RepoRoot();
        var appXaml = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml"));

        appXaml.ShouldNotContain("StartupUri=");
    }

    [Fact]
    public void Host_bootstrap_emits_pre_login_milestones()
    {
        using var temp = new TemporaryDirectory();
        var runtimeRoot = Path.Combine(temp.Path, "runtime-root");
        var appsettings = Path.Combine(temp.Path, "appsettings.json");
        WriteBootstrapAppSettings(appsettings, runtimeRoot);

        var result = HostBootstrapper.Bootstrap(new HostBootstrapOptions(
            ShowMainWindow: false,
            PackagedAppSettingsPath: appsettings,
            RuntimeConfigDirectory: Path.Combine(temp.Path, "config"),
            EnvironmentName: "Test"));

        result.Milestones.Select(milestone => milestone.Name).ShouldBe([
            StartupMilestoneNames.HostBuilt,
            StartupMilestoneNames.ConfigLoaded,
            StartupMilestoneNames.PersistenceOpened,
            StartupMilestoneNames.LoginVisible
        ]);
    }

    [Fact]
    public void App_defers_duplicate_login_runtime_migration_and_plugin_migration_until_after_first_paint()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));

        appSource.ShouldContain("applyPreLoginMigrations: false");
        appSource.ShouldContain("window.ContentRendered += async");
        appSource.ShouldContain("var pluginMigration = loginRuntime.PluginRuntimeCoordinator.ApplyPostLoginMigrations();");

        var contentRendered = appSource.IndexOf("window.ContentRendered += async", StringComparison.Ordinal);
        var pluginMigration = appSource.IndexOf(
            "var pluginMigration = loginRuntime.PluginRuntimeCoordinator.ApplyPostLoginMigrations();",
            StringComparison.Ordinal);
        var showDialog = appSource.IndexOf("window.ShowDialog();", StringComparison.Ordinal);

        contentRendered.ShouldBeGreaterThanOrEqualTo(0);
        pluginMigration.ShouldBeGreaterThan(contentRendered);
        showDialog.ShouldBeGreaterThan(pluginMigration);
    }

    [Fact]
    public void Host_login_runtime_factory_defers_license_device_fingerprint_until_license_use()
    {
        var root = RepoRoot();
        var factorySource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Login", "HostLoginRuntimeFactory.cs"));

        factorySource.ShouldContain("new Lazy<string>(() => CreateCurrentLicenseDeviceFingerprint(protectedMaterial.StableDeviceId))");
        factorySource.ShouldContain("() => currentLicenseDeviceFingerprint.Value");
        factorySource.ShouldNotContain("var currentLicenseDeviceFingerprint = CreateCurrentLicenseDeviceFingerprint");
    }

    [Fact]
    public void App_defers_settings_about_and_initial_checkpoint_until_after_shell_first_paint()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));
        var mainWindowSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));

        var contentRendered = appSource.IndexOf("window.ContentRendered += async", StringComparison.Ordinal);
        var configureSettings = appSource.IndexOf(
            "ConfigureSettingsAndAbout(shellViewModel, strings, loginRuntime, authenticatedSession?.UserId);",
            StringComparison.Ordinal);
        var showDialog = appSource.IndexOf("window.ShowDialog();", StringComparison.Ordinal);

        contentRendered.ShouldBeGreaterThanOrEqualTo(0);
        configureSettings.ShouldBeGreaterThan(contentRendered);
        configureSettings.ShouldBeLessThan(showDialog);
        mainWindowSource.ShouldContain("Dispatcher.BeginInvoke");
        mainWindowSource.ShouldContain("(Action)(() => CheckpointSession())");
    }

    [Fact]
    public void Host_bootstrap_acquires_single_instance_before_login_visible_and_exits_on_second_instance()
    {
        using var temp = new TemporaryDirectory();
        var runtimeRoot = Path.Combine(temp.Path, "runtime-root");
        var appsettings = Path.Combine(temp.Path, "appsettings.json");
        WriteBootstrapAppSettings(appsettings, runtimeRoot);
        var singleInstance = new RecordingSingleInstanceCoordinator(isFirstInstance: false);

        var result = HostBootstrapper.Bootstrap(new HostBootstrapOptions(
            ShowMainWindow: false,
            PackagedAppSettingsPath: appsettings,
            RuntimeConfigDirectory: Path.Combine(temp.Path, "config"),
            EnvironmentName: "Test",
            SingleInstanceCoordinatorFactory: () => singleInstance));

        singleInstance.AcquireCalls.ShouldBe(1);
        singleInstance.HandOffCalls.ShouldBe(1);
        result.Milestones.Select(milestone => milestone.Name).ShouldNotContain(StartupMilestoneNames.LoginVisible);
    }

    [Fact]
    public void Host_bootstrap_records_pre_login_instrumentation_before_login_visible()
    {
        using var temp = new TemporaryDirectory();
        var runtimeRoot = Path.Combine(temp.Path, "runtime-root");
        var appsettings = Path.Combine(temp.Path, "appsettings.json");
        WriteBootstrapAppSettings(appsettings, runtimeRoot);
        var instrumentation = new PreLoginInstrumentation();

        var result = HostBootstrapper.Bootstrap(new HostBootstrapOptions(
            ShowMainWindow: false,
            PackagedAppSettingsPath: appsettings,
            RuntimeConfigDirectory: Path.Combine(temp.Path, "config"),
            EnvironmentName: "Test",
            PreLoginInstrumentation: instrumentation));

        instrumentation.Events.ShouldBe(["startup.pre-auth-metadata-complete"]);
        result.Milestones.Select(milestone => milestone.Name).ShouldContain(StartupMilestoneNames.LoginVisible);
    }

    [Fact]
    public void Host_bootstrap_validates_config_and_prepares_runtime_folders()
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

        Directory.Exists(Path.Combine(runtimeRoot, "data")).ShouldBeTrue();
        Directory.Exists(Path.Combine(runtimeRoot, "logs")).ShouldBeTrue();
    }

    [Fact]
    public void Host_bootstrap_writes_runtime_folder_diagnostics_to_local_sink()
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

        var contents = ReadSingleLogContents(runtimeRoot);
        contents.ShouldContain("Runtime folder ready:");
        contents.ShouldContain(runtimeRoot);
    }

    [Fact]
    public void Host_bootstrap_emits_unknown_dobby_section_diagnostic_without_blocking_startup()
    {
        using var temp = new TemporaryDirectory();
        var runtimeRoot = Path.Combine(temp.Path, "runtime-root");
        var appsettings = Path.Combine(temp.Path, "appsettings.json");
        WriteBootstrapAppSettings(appsettings, runtimeRoot, "\"Unexpected\": { \"Ignored\": true },");

        HostBootstrapper.Bootstrap(new HostBootstrapOptions(
            ShowMainWindow: false,
            PackagedAppSettingsPath: appsettings,
            RuntimeConfigDirectory: Path.Combine(temp.Path, "config"),
            EnvironmentName: "Test"));

        var contents = ReadSingleLogContents(runtimeRoot);
        contents.ShouldContain("Unknown Dobby configuration section ignored:");
        contents.ShouldContain("Unexpected");
    }

    [Fact]
    public void Host_bootstrap_can_record_diagnostics_export_audit_event()
    {
        IAuditTrail trail = new InMemoryAuditTrail();

        HostBootstrapper.RecordDiagnosticsExportCreated(trail, "support-package.zip");

        var auditEvent = trail.Snapshot().Single();
        auditEvent.EventName.ShouldBe(AuditEventName.DiagnosticsExportCreated);
        auditEvent.Result.ShouldBe(AuditResult.Success);
    }

    [Fact]
    public void Host_bootstrap_fails_closed_when_pre_login_persistence_does_not_open()
    {
        using var temp = new TemporaryDirectory();
        var runtimeRoot = Path.Combine(temp.Path, "runtime-root");
        var appsettings = Path.Combine(temp.Path, "appsettings.json");
        WriteBootstrapAppSettings(appsettings, runtimeRoot);

        var exception = Should.Throw<InvalidOperationException>(() =>
            HostBootstrapper.Bootstrap(new HostBootstrapOptions(
                ShowMainWindow: false,
                PackagedAppSettingsPath: appsettings,
                RuntimeConfigDirectory: Path.Combine(temp.Path, "config"),
                EnvironmentName: "Test",
                PreLoginPersistenceOpen: _ => false)));

        exception.Message.ShouldContain("persistence", Case.Insensitive);
    }

    [Fact]
    public void Host_bootstrap_fails_closed_without_post_persistence_instrumentation_when_provider_is_disabled()
    {
        using var temp = new TemporaryDirectory();
        var runtimeRoot = Path.Combine(temp.Path, "runtime-root");
        var appsettings = Path.Combine(temp.Path, "appsettings.json");
        WriteBootstrapAppSettings(
            appsettings,
            runtimeRoot,
            storageProviderMode: "ProviderNeutralSkeleton");
        var instrumentation = new PreLoginInstrumentation();

        Should.Throw<InvalidOperationException>(() =>
            HostBootstrapper.Bootstrap(new HostBootstrapOptions(
                ShowMainWindow: false,
                PackagedAppSettingsPath: appsettings,
                RuntimeConfigDirectory: Path.Combine(temp.Path, "config"),
                EnvironmentName: "Test",
                PreLoginInstrumentation: instrumentation)));

        instrumentation.Events.ShouldBeEmpty();
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }

    private static void WriteBootstrapAppSettings(
        string appsettingsPath,
        string runtimeRoot,
        string additionalDobbySections = "",
        string storageProviderMode = "SQLite3MultipleCiphers")
    {
        File.WriteAllText(appsettingsPath, $$"""
            {
              "Dobby": {
                {{additionalDobbySections}}
                "Runtime": { "DataRoot": "{{runtimeRoot.Replace("\\", "\\\\")}}", "EnvironmentName": "Test" },
                "Storage": { "ProviderMode": "{{storageProviderMode}}", "RequireDataAtRestProtection": true, "BusyTimeoutMilliseconds": 5000 },
                "Auth": { "LoginThrottleStepsSeconds": [0, 0, 10, 30, 60], "PasswordKdfMode": "Pbkdf2Sha256" },
                "Session": { "AbsoluteTimeoutMinutes": 480, "IdleTimeoutMinutes": 30, "AutoLockEnabledByDefault": false },
                "Diagnostics": { "TechnicalLogRetentionDays": 30, "TechnicalLogTotalSizeCapMegabytes": 256 },
                "Audit": { "RetentionDays": 365 },
                "Localization": { "DefaultCulture": "az-AZ", "SupportedCultures": ["az-AZ", "en-US", "tr-TR"] },
                "Plugins": { "AllowPreLoginDiscovery": false },
                "Licensing": { "ImportRequiresPermission": true },
                "BranchShared": { "RuntimeEnabled": false }
              },
              "Serilog": { "MinimumLevel": "Information" }
            }
            """);
    }

    private static string ReadSingleLogContents(string runtimeRoot)
    {
        var logFile = Directory.EnumerateFiles(Path.Combine(runtimeRoot, "logs"), "*.log").Single();
        return File.ReadAllText(logFile);
    }

    private sealed class RecordingSingleInstanceCoordinator(bool isFirstInstance) : ISingleInstanceCoordinator
    {
        public int AcquireCalls { get; private set; }

        public int HandOffCalls { get; private set; }

        public SingleInstanceAcquireResult Acquire()
        {
            AcquireCalls++;
            return isFirstInstance
                ? SingleInstanceAcquireResult.FirstInstance()
                : SingleInstanceAcquireResult.SecondInstance();
        }

        public void HandOffToFirstInstance()
        {
            HandOffCalls++;
        }
    }
}

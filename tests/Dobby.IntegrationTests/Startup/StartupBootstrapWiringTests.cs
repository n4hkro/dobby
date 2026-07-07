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
    public void App_xaml_does_not_preload_theme_before_startup_snapshot_is_loaded()
    {
        var root = RepoRoot();
        var appXaml = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml"));

        appXaml.ShouldContain("DesignTokens.xaml");
        appXaml.ShouldNotContain("LightTheme.xaml");
        appXaml.ShouldNotContain("DarkTheme.xaml");
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
    public void App_uses_lightweight_pre_login_runtime_until_authenticated_session_exists()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));

        appSource.ShouldContain("HostBootstrapper.BootstrapLoginWindow(");
        appSource.ShouldContain("InitializeLoginEntryRuntimeAsync(loginWindow)");
        appSource.ShouldContain("LoginStartupSnapshotLoader.Load()");
        appSource.ShouldContain("ShellThemeCoordinator.ApplyTheme(this, startupSnapshot.Theme)");
        appSource.ShouldContain("preparedLoginEntryRuntimeTask");
        appSource.ShouldContain("PostLoginShellPresenter.ShowAsync(");

        var startupSnapshot = appSource.IndexOf("var startupSnapshot = LoginStartupSnapshotLoader.Load()", StringComparison.Ordinal);
        var themeApply = appSource.IndexOf("ShellThemeCoordinator.ApplyTheme(this, startupSnapshot.Theme)", StringComparison.Ordinal);
        var loginWindow = appSource.IndexOf("new LoginWindow(startupSnapshot)", StringComparison.Ordinal);
        var showDialog = appSource.IndexOf("loginWindow.ShowDialog()", StringComparison.Ordinal);
        var contentRendered = appSource.IndexOf("loginWindow.ContentRendered", StringComparison.Ordinal);
        var presenter = appSource.IndexOf("PostLoginShellPresenter.ShowAsync(", showDialog, StringComparison.Ordinal);

        startupSnapshot.ShouldBeGreaterThanOrEqualTo(0);
        themeApply.ShouldBeGreaterThan(startupSnapshot);
        loginWindow.ShouldBeGreaterThanOrEqualTo(0);
        loginWindow.ShouldBeGreaterThan(themeApply);
        contentRendered.ShouldBeGreaterThan(loginWindow);
        showDialog.ShouldBeGreaterThan(loginWindow);
        presenter.ShouldBeGreaterThan(showDialog);

        var beforeLoginShow = appSource[..showDialog];
        beforeLoginShow.ShouldNotContain("HostLoginRuntimeFactory.Create(");
        beforeLoginShow.ShouldNotContain("HostLoginRuntimeFactory.CreatePreLogin(");
        beforeLoginShow.ShouldNotContain("new WorkSessionRegistry()");
        beforeLoginShow.ShouldNotContain("OpenPreLoginPersistence");
        beforeLoginShow.ShouldNotContain("ApplyPreLoginMigrations");
    }

    [Fact]
    public void Login_startup_snapshot_loader_reads_only_theme_and_remember_me_prefill_before_first_paint()
    {
        var root = RepoRoot();
        var loaderPath = Path.Combine(root.FullName, "src", "Dobby.Host", "Login", "LoginStartupSnapshotLoader.cs");
        File.Exists(loaderPath).ShouldBeTrue();
        var loaderSource = File.ReadAllText(loaderPath);

        loaderSource.ShouldContain("StartupThemeSnapshotStore.LoadTheme(");
        loaderSource.ShouldContain("StartupRememberMeSnapshotStore.LoadSavedLoginPrefill(");
        loaderSource.ShouldNotContain("StartupRememberMeSnapshotStore.LoadSavedLogin()");
        loaderSource.ShouldNotContain("HostConfigurationSources");
        loaderSource.ShouldNotContain("DobbyOptions");
        loaderSource.ShouldNotContain("ProtectedMaterialAvailabilityProbe");
        loaderSource.ShouldNotContain("EncryptedSqliteConnectionFactory");
        loaderSource.ShouldNotContain("HostPersistenceCommandExecutor");
        loaderSource.ShouldNotContain("RememberMeSlotService");
        loaderSource.ShouldNotContain("HostLoginRuntimeFactory");
        loaderSource.ShouldNotContain("LoginService");
        loaderSource.ShouldNotContain("SessionLifecycleService");
        loaderSource.ShouldNotContain("RecoveryRequestExporter");
        loaderSource.ShouldNotContain("Plugin");
        loaderSource.ShouldNotContain("License");
        loaderSource.ShouldNotContain("AuditRetentionMaintenance");
    }

    [Fact]
    public void Login_startup_snapshot_stores_use_source_generated_json_metadata()
    {
        var root = RepoRoot();
        var rememberStore = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "StartupRememberMeSnapshotStore.cs"));
        var themeStore = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "StartupThemeSnapshotStore.cs"));
        var context = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "HostStartupJsonSerializerContext.cs"));

        rememberStore.ShouldContain("File.ReadAllBytes(path)");
        rememberStore.ShouldContain("HostStartupJsonSerializerContext.Default.StartupRememberMeSnapshot");
        rememberStore.ShouldContain("JsonSerializer.SerializeToUtf8Bytes(");
        themeStore.ShouldContain("File.ReadAllBytes(path)");
        themeStore.ShouldContain("HostStartupJsonSerializerContext.Default.StartupThemeSnapshot");
        themeStore.ShouldContain("JsonSerializer.SerializeToUtf8Bytes(");
        context.ShouldContain("[JsonSerializable(typeof(StartupThemeSnapshot))]");
        context.ShouldContain("[JsonSerializable(typeof(StartupRememberMeSnapshot))]");
    }

    [Fact]
    public void App_login_startup_keeps_shell_plugin_license_imports_out_of_app_codebehind()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));

        appSource.ShouldNotContain("using Dobby.Contracts");
        appSource.ShouldNotContain("using Dobby.Diagnostics");
        appSource.ShouldNotContain("using Dobby.Host.Plugins");
        appSource.ShouldNotContain("using Dobby.Host.Settings");
        appSource.ShouldNotContain("using Dobby.Navigation");
        appSource.ShouldNotContain("using Dobby.PluginFramework");
        appSource.ShouldNotContain("using Dobby.Security.Authorization");
        appSource.ShouldNotContain("using Dobby.Shell");
        appSource.ShouldNotContain("using Dobby.Shell.Layout");
        appSource.ShouldNotContain("using Dobby.Shell.UiDispatch");
        appSource.ShouldNotContain("using Dobby.Shell.WorkSessions");
    }

    [Fact]
    public void App_attaches_pre_login_runtime_before_background_shell_runtime_is_needed()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));
        var runtimeSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Login", "LoginRuntimePreparer.cs"));

        appSource.ShouldContain("loginWindow.ContentRendered += (_, _) =>");
        appSource.ShouldContain("preparedLoginEntryRuntimeTask ??= LoginRuntimePreparer.InitializeLoginEntryRuntimeAsync(loginWindow);");
        appSource.ShouldContain("preparedLoginEntryRuntime.ShellRuntimeTask");
        appSource.ShouldContain("ActivateExisting(authenticatedSession)");
        appSource.ShouldContain("loginWindow.AttachDeferredWorkflow(new DeferredLoginRuntimeWorkflow(EnsureLoginEntryRuntimeAsync));");
        runtimeSource.ShouldContain("HostLoginRuntimeFactory.CreatePreLogin(");
        runtimeSource.ShouldContain("loginWindow.AttachRuntime(preLoginRuntime)");
        runtimeSource.ShouldContain("PreLoginRuntime");
        runtimeSource.ShouldContain("HostLoginRuntimeFactory.Create(");
        runtimeSource.ShouldContain("new WorkSessionRegistry()");

        var loginShow = appSource.IndexOf("loginWindow.ShowDialog()", StringComparison.Ordinal);
        var deferred = appSource.IndexOf("loginWindow.AttachDeferredWorkflow(", StringComparison.Ordinal);
        var shellPresenter = appSource.IndexOf("PostLoginShellPresenter.ShowAsync(", loginShow, StringComparison.Ordinal);
        deferred.ShouldBeGreaterThanOrEqualTo(0);
        deferred.ShouldBeLessThan(loginShow);
        var afterLoginBeforeShell = appSource[loginShow..shellPresenter];
        afterLoginBeforeShell.ShouldNotContain("HostLoginRuntimeFactory.Create(");
        afterLoginBeforeShell.ShouldContain("await preparedLoginEntryRuntime.ShellRuntimeTask");
    }

    [Fact]
    public void App_defers_login_runtime_warmup_until_after_login_first_paint_flush_is_queued()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));

        var contentRendered = appSource.IndexOf("loginWindow.ContentRendered += (_, _) =>", StringComparison.Ordinal);
        var flush = appSource.IndexOf("startupTiming.FlushAfterFirstPaintAsync()", contentRendered, StringComparison.Ordinal);
        var warmup = appSource.IndexOf("_ = EnsureLoginEntryRuntimeAsync();", contentRendered, StringComparison.Ordinal);

        contentRendered.ShouldBeGreaterThanOrEqualTo(0);
        flush.ShouldBeGreaterThan(contentRendered);
        warmup.ShouldBeGreaterThan(flush);
    }

    [Fact]
    public void App_records_login_ready_timing_when_deferred_workflow_enables_button()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));
        var traceSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Login", "LoginStartupTimingTrace.cs"));

        appSource.ShouldContain("loginWindow.AttachDeferredWorkflow(new DeferredLoginRuntimeWorkflow(EnsureLoginEntryRuntimeAsync));");
        appSource.ShouldContain("startupTiming.MarkLoginReady()");
        traceSource.ShouldContain("ready_ms=");
    }

    [Fact]
    public void App_login_cancel_schedules_runtime_cleanup_without_blocking_shutdown()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));
        var runtimeSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Login", "LoginRuntimePreparer.cs"));

        appSource.ShouldContain("LoginRuntimePreparer.DisposeWhenReadyWithoutBlockingShutdown(preparedLoginEntryRuntimeTask);");
        runtimeSource.ShouldContain("public static void DisposeWhenReadyWithoutBlockingShutdown(Task<PreparedLoginEntryRuntime?>? preparedRuntimeTask)");
        runtimeSource.ShouldContain("_ = DisposeWhenReadyAsync(preparedRuntimeTask)");

        var cancelBlockStart = appSource.IndexOf("if (loginWindow.ShowDialog() != true)", StringComparison.Ordinal);
        var cancelBlockEnd = appSource.IndexOf("var preparedLoginEntryRuntime = await", cancelBlockStart, StringComparison.Ordinal);
        cancelBlockStart.ShouldBeGreaterThanOrEqualTo(0);
        cancelBlockEnd.ShouldBeGreaterThan(cancelBlockStart);

        var cancelBlock = appSource[cancelBlockStart..cancelBlockEnd];
        cancelBlock.ShouldNotContain("await LoginRuntimePreparer.DisposeWhenReadyAsync(preparedLoginEntryRuntimeTask)");
        cancelBlock.IndexOf(
                "LoginRuntimePreparer.DisposeWhenReadyWithoutBlockingShutdown(preparedLoginEntryRuntimeTask);",
                StringComparison.Ordinal)
            .ShouldBeLessThan(cancelBlock.IndexOf("ShutdownHostProcess();", StringComparison.Ordinal));
        appSource.ShouldContain("ProcessExitEscalation.Begin();");

        var shutdownHelperStart = appSource.IndexOf("private void ShutdownHostProcess()", StringComparison.Ordinal);
        var shutdownHelperEnd = appSource.IndexOf("protected override void OnExit", shutdownHelperStart, StringComparison.Ordinal);
        shutdownHelperStart.ShouldBeGreaterThanOrEqualTo(0);
        shutdownHelperEnd.ShouldBeGreaterThan(shutdownHelperStart);

        var shutdownHelper = appSource[shutdownHelperStart..shutdownHelperEnd];
        shutdownHelper.IndexOf("ProcessExitEscalation.Begin();", StringComparison.Ordinal)
            .ShouldBeLessThan(shutdownHelper.IndexOf("Shutdown();", StringComparison.Ordinal));
    }

    [Fact]
    public void App_records_login_first_paint_timing_without_blocking_login_window_creation()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));
        var tracePath = Path.Combine(root.FullName, "src", "Dobby.Host", "Login", "LoginStartupTimingTrace.cs");

        File.Exists(tracePath).ShouldBeTrue();
        var traceSource = File.ReadAllText(tracePath);

        appSource.ShouldContain("var firstLoginStartupTiming = LoginStartupTimingTrace.Start()");
        appSource.ShouldContain("firstLoginStartupTiming.MarkBootstrapCompleted()");
        appSource.ShouldContain("await RunLoginLoopAsync(firstLoginStartupTiming)");
        appSource.ShouldContain("var startupTiming = firstLoginStartupTiming ?? LoginStartupTimingTrace.Start()");
        appSource.ShouldContain("startupTiming.MarkSnapshotLoaded()");
        appSource.ShouldContain("startupTiming.MarkThemeApplied()");
        appSource.ShouldContain("startupTiming.MarkLoginWindowConstructed()");
        appSource.ShouldContain("startupTiming.MarkLoginContentRendered()");
        appSource.ShouldContain("startupTiming.FlushAfterFirstPaintAsync()");
        appSource.ShouldContain("DispatcherPriority.ContextIdle");
        traceSource.ShouldContain("Stopwatch.GetTimestamp()");
        traceSource.ShouldContain("Stopwatch.GetElapsedTime(");
        traceSource.ShouldContain("login-startup-timing.log");
        traceSource.ShouldContain("File.AppendAllText");
        traceSource.ShouldContain("bootstrap_ms=");

        var start = appSource.IndexOf("var startupTiming = firstLoginStartupTiming ?? LoginStartupTimingTrace.Start()", StringComparison.Ordinal);
        var snapshot = appSource.IndexOf("var startupSnapshot = LoginStartupSnapshotLoader.Load()", StringComparison.Ordinal);
        var contentRendered = appSource.IndexOf("loginWindow.ContentRendered += (_, _) =>", StringComparison.Ordinal);
        var flush = appSource.IndexOf("startupTiming.FlushAfterFirstPaintAsync()", StringComparison.Ordinal);
        var showDialog = appSource.IndexOf("loginWindow.ShowDialog()", StringComparison.Ordinal);

        start.ShouldBeGreaterThanOrEqualTo(0);
        snapshot.ShouldBeGreaterThan(start);
        contentRendered.ShouldBeGreaterThan(snapshot);
        flush.ShouldBeGreaterThan(contentRendered);
        showDialog.ShouldBeGreaterThan(flush);

        var beforeLoginWindowCreated = appSource[..appSource.IndexOf("var loginWindow = new LoginWindow(startupSnapshot)", StringComparison.Ordinal)];
        beforeLoginWindowCreated.ShouldNotContain("FlushAfterFirstPaintAsync");
        beforeLoginWindowCreated.ShouldNotContain("File.AppendAllText");
    }

    [Fact]
    public void Minimal_login_window_bootstrap_only_allows_single_instance_before_login_visible()
    {
        var singleInstance = new RecordingSingleInstanceCoordinator(isFirstInstance: true);
        var result = HostBootstrapper.BootstrapLoginWindow(new HostLoginWindowBootstrapOptions(
            SingleInstanceCoordinatorFactory: () => singleInstance));

        singleInstance.AcquireCalls.ShouldBe(1);
        singleInstance.HandOffCalls.ShouldBe(0);
        result.Milestones.Select(milestone => milestone.Name).ShouldBe([
            StartupMilestoneNames.HostBuilt,
            StartupMilestoneNames.LoginVisible
        ]);
    }

    [Fact]
    public void Minimal_login_window_bootstrap_has_no_config_diagnostics_or_persistence_work()
    {
        var root = RepoRoot();
        var startupSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Startup", "HostBootstrapper.cs"));
        var methodStart = startupSource.IndexOf("public static HostBootstrapResult BootstrapLoginWindow(", StringComparison.Ordinal);
        methodStart.ShouldBeGreaterThanOrEqualTo(0);
        var nextMethod = startupSource.IndexOf("public static StartupPlan DescribeStartupPlan()", methodStart, StringComparison.Ordinal);
        if (nextMethod < 0)
        {
            nextMethod = startupSource.IndexOf("public static HostBootstrapResult Bootstrap(", methodStart, StringComparison.Ordinal);
        }

        var methodBody = nextMethod < 0
            ? startupSource[methodStart..]
            : startupSource[methodStart..nextMethod];
        methodBody.ShouldContain("SingleInstanceCoordinatorFactory");
        methodBody.ShouldNotContain("HostConfigurationSources.Build");
        methodBody.ShouldNotContain("LocalDiagnosticsConfigurator");
        methodBody.ShouldNotContain("RuntimeFolderPreparer");
        methodBody.ShouldNotContain("OpenPreLoginPersistence");
        methodBody.ShouldNotContain("ProtectedMaterialAvailabilityProbe");
    }

    [Fact]
    public void App_listens_for_second_instance_activation_and_restores_current_window()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));

        appSource.ShouldContain("singleInstanceActivationListener");
        appSource.ShouldContain("StartActivationListener(ActivateCurrentWindow)");
        appSource.ShouldContain("private void ActivateCurrentWindow()");
        appSource.ShouldContain("Dispatcher.BeginInvoke");
        appSource.ShouldContain("MainWindow");
        appSource.ShouldContain("WindowActivationHelper.Activate(window)");
        appSource.ShouldNotContain("window.Topmost = true");
        appSource.ShouldNotContain("window.Activate();");
    }

    [Fact]
    public void Window_activation_helper_restores_and_foregrounds_existing_window()
    {
        var root = RepoRoot();
        var helperPath = Path.Combine(root.FullName, "src", "Dobby.Host", "WindowActivationHelper.cs");

        File.Exists(helperPath).ShouldBeTrue();
        var helperSource = File.ReadAllText(helperPath);

        helperSource.ShouldContain("ShowWindow(");
        helperSource.ShouldContain("SetForegroundWindow(");
        helperSource.ShouldContain("WindowInteropHelper");
        helperSource.ShouldContain("WindowState.Minimized");
        helperSource.ShouldContain("window.Activate()");
    }

    [Fact]
    public void App_runs_post_login_shell_warmup_off_the_shell_content_rendered_ui_handler()
    {
        var root = RepoRoot();
        var shellPresenterSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));

        shellPresenterSource.ShouldContain("window.ContentRendered += (_, _) =>");
        shellPresenterSource.ShouldContain("_ = RunPostLoginShellWarmupAsync(");
        shellPresenterSource.ShouldContain("Task.Run(() => loginRuntime.PluginRuntimeCoordinator.ApplyPostLoginMigrations())");
        shellPresenterSource.ShouldContain("Task.Run(() => BuildSettingsAndAbout(");
        shellPresenterSource.ShouldContain("Dispatcher.InvokeAsync");
        shellPresenterSource.ShouldContain("BeginPostLoginActivation");
        shellPresenterSource.ShouldNotContain("window.ContentRendered += async");
        shellPresenterSource.ShouldNotContain("var pluginMigration = loginRuntime.PluginRuntimeCoordinator.ApplyPostLoginMigrations();");

        var contentRendered = shellPresenterSource.IndexOf(
            "window.ContentRendered += (_, _) =>",
            StringComparison.Ordinal);
        var warmup = shellPresenterSource.IndexOf("private static async Task RunPostLoginShellWarmupAsync", StringComparison.Ordinal);
        var showDialog = shellPresenterSource.IndexOf("window.ShowDialog();", StringComparison.Ordinal);

        contentRendered.ShouldBeGreaterThanOrEqualTo(0);
        warmup.ShouldBeGreaterThan(contentRendered);
        showDialog.ShouldBeGreaterThan(contentRendered);
    }

    [Fact]
    public void App_defers_user_shell_preference_load_until_after_shell_first_paint()
    {
        var root = RepoRoot();
        var shellPresenterSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));
        var showDialog = shellPresenterSource.IndexOf("window.ShowDialog();", StringComparison.Ordinal);
        var contentRendered = shellPresenterSource.IndexOf(
            "window.ContentRendered += (_, _) =>",
            StringComparison.Ordinal);
        var snapshotSave = shellPresenterSource.IndexOf(
            "StartupThemeSnapshotStore.SaveTheme(currentTheme)",
            StringComparison.Ordinal);
        var preferenceLoad = shellPresenterSource.IndexOf(
            "scopedPreferenceSource.Load()",
            StringComparison.Ordinal);

        showDialog.ShouldBeGreaterThanOrEqualTo(0);
        contentRendered.ShouldBeGreaterThanOrEqualTo(0);
        snapshotSave.ShouldBeGreaterThan(contentRendered);
        preferenceLoad.ShouldBeGreaterThan(contentRendered);

        var beforeShow = shellPresenterSource[..showDialog];
        beforeShow.ShouldNotContain("scopedPreferenceSource.Load()");
        beforeShow.ShouldNotContain("ShellThemeCoordinator.ApplyTheme(application, currentTheme)");
        beforeShow.ShouldNotContain("themeCoordinator.CurrentTheme");
        beforeShow.ShouldNotContain("StartupThemeSnapshotStore.SaveTheme(currentTheme)");
    }

    [Fact]
    public void Main_window_attach_runtime_does_not_reapply_layout_or_start_checkpoint_before_show()
    {
        var root = RepoRoot();
        var mainWindowSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var attachStart = mainWindowSource.IndexOf("public void AttachRuntime(", StringComparison.Ordinal);
        var nextMethod = mainWindowSource.IndexOf("public void ShowRuntimeInitializationFailure()", attachStart, StringComparison.Ordinal);

        attachStart.ShouldBeGreaterThanOrEqualTo(0);
        nextMethod.ShouldBeGreaterThan(attachStart);

        var attachBlock = mainWindowSource[attachStart..nextMethod];
        attachBlock.ShouldContain("initialPreference = runtimeInitialPreference;");
        attachBlock.ShouldContain("initialPreferenceExplicitlyProvided = true;");
        attachBlock.ShouldNotContain("UpdateWorkSessionWarningVisibility();");
        attachBlock.ShouldNotContain("ApplyInitialShellPreference();");
        attachBlock.ShouldNotContain("StartSessionCheckpointTimer();");

        mainWindowSource.ShouldContain("if (initialPreferenceExplicitlyProvided)");
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
    public void Host_login_runtime_factory_backfills_startup_remember_me_snapshot_after_runtime_load()
    {
        var root = RepoRoot();
        var factorySource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Login", "HostLoginRuntimeFactory.cs"));
        var compactFactorySource = string.Concat(factorySource.Where(character => !char.IsWhiteSpace(character)));

        factorySource.ShouldContain("StartupRememberMeSnapshotSink.Instance");
        compactFactorySource.ShouldContain(
            "newRememberMeSlotService(executor,rememberScopeProtector,applicationEntropy,StartupRememberMeSnapshotSink.Instance)");
    }

    [Fact]
    public void App_defers_settings_about_and_does_not_checkpoint_immediately_on_shell_load()
    {
        var root = RepoRoot();
        var shellPresenterSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));
        var mainWindowSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));

        var contentRendered = shellPresenterSource.IndexOf(
            "window.ContentRendered += (_, _) =>",
            StringComparison.Ordinal);
        var configureSettings = shellPresenterSource.IndexOf(
            "BuildSettingsAndAbout(strings, loginRuntime, userId)",
            StringComparison.Ordinal);
        var showDialog = shellPresenterSource.IndexOf("window.ShowDialog();", StringComparison.Ordinal);

        contentRendered.ShouldBeGreaterThanOrEqualTo(0);
        configureSettings.ShouldBeGreaterThan(contentRendered);
        showDialog.ShouldBeGreaterThan(contentRendered);
        mainWindowSource.ShouldContain("sessionTimer.Tick += (_, _) => CheckpointSession();");
        mainWindowSource.ShouldNotContain("(Action)(() => CheckpointSession())");
    }

    [Fact]
    public void App_records_post_login_submit_to_shell_first_paint_timing_without_blocking_paint()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));
        var loginWindowSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Login", "LoginWindow.xaml.cs"));
        var shellPresenterSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));
        var traceSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Login", "PostLoginTimingTrace.cs"));

        loginWindowSource.ShouldContain("PostLoginTimingTrace.Start()");
        loginWindowSource.ShouldContain("postLoginTiming.MarkLoginSubmitCompleted()");
        appSource.ShouldContain("var postLoginTiming = loginWindow.PostLoginTiming;");
        appSource.ShouldContain("postLoginTiming?.MarkRuntimeReady();");
        appSource.ShouldContain("postLoginTiming");
        shellPresenterSource.ShouldContain("PostLoginTimingTrace? postLoginTiming = null");
        shellPresenterSource.ShouldContain("postLoginTiming?.MarkShellCompositionStarted();");
        shellPresenterSource.ShouldContain("postLoginTiming?.MarkShellWindowConstructed();");
        shellPresenterSource.ShouldContain("postLoginTiming?.MarkShellRuntimeAttached();");
        shellPresenterSource.ShouldContain("postLoginTiming?.MarkShellFirstPaint();");
        shellPresenterSource.ShouldContain("postLoginTiming.FlushAfterShellFirstPaintAsync()");
        traceSource.ShouldContain("post-login-timing.log");
        traceSource.ShouldContain("Stopwatch.GetTimestamp()");
        traceSource.ShouldNotContain("Username");
        traceSource.ShouldNotContain("Password");
    }

    [Fact]
    public void App_shows_shell_without_creating_post_login_runtime_after_success()
    {
        var root = RepoRoot();
        var appSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));

        var loginShowDialog = appSource.IndexOf("loginWindow.ShowDialog()", StringComparison.Ordinal);
        var fullRuntime = appSource.IndexOf("HostLoginRuntimeFactory.Create(", loginShowDialog, StringComparison.Ordinal);
        var shellPresenter = appSource.IndexOf("PostLoginShellPresenter.ShowAsync(", loginShowDialog, StringComparison.Ordinal);

        loginShowDialog.ShouldBeGreaterThanOrEqualTo(0);
        shellPresenter.ShouldBeGreaterThan(loginShowDialog);
        fullRuntime.ShouldBe(-1);

        var afterLoginBeforeShell = appSource[loginShowDialog..shellPresenter];
        afterLoginBeforeShell.ShouldNotContain("HostLoginRuntimeFactory.Create(");
        afterLoginBeforeShell.ShouldNotContain("SettingsPreferenceSourceFactory");
        afterLoginBeforeShell.ShouldNotContain("PluginPageRouteCoordinator");
        afterLoginBeforeShell.ShouldNotContain("BuildSettingsAndAbout(");
        afterLoginBeforeShell.ShouldNotContain("PluginRuntimeCoordinator.ApplyPostLoginMigrations");
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

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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

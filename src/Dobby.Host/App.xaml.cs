using System.Windows;
using Dobby.Application;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.Policies;
using Dobby.Diagnostics;
using Dobby.Host.Login;
using Dobby.Host.Plugins;
using Dobby.Host.Settings;
using Dobby.Host.Startup;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.Security.Authorization;
using Dobby.Shell;
using Dobby.Shell.Layout;
using Dobby.Shell.UiDispatch;
using Dobby.Shell.WorkSessions;

namespace Dobby.Host;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? singleInstanceCoordinator;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (CrashRecoverySmokeHook.TryRunFromEnvironment(out var smokeExitCode))
        {
            Shutdown(smokeExitCode);
            return;
        }

        singleInstanceCoordinator = new SingleInstanceCoordinator();
        var bootstrap = HostBootstrapper.Bootstrap(new HostBootstrapOptions(
            SingleInstanceCoordinatorFactory: () => singleInstanceCoordinator));
        if (!bootstrap.Milestones.Any(milestone => milestone.Name == StartupMilestoneNames.LoginVisible))
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        await RunLoginLoopAsync();
    }

    private async Task RunLoginLoopAsync()
    {
        while (true)
        {
            await using var loginRuntime = HostLoginRuntimeFactory.Create();
            var loginWindow = new LoginWindow(loginRuntime.LoginWorkflow, loginRuntime.LoginViewModel, loginRuntime.RecoveryViewModel);
            MainWindow = loginWindow;
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            var authenticatedSession = loginWindow.AuthenticatedSession;
            var pluginMigration = loginRuntime.PluginRuntimeCoordinator.ApplyPostLoginMigrations();
            if (!pluginMigration.IsSuccess)
            {
                throw new InvalidOperationException(pluginMigration.Error.Message);
            }
            var scopedPreferenceSource = authenticatedSession is null
                ? loginRuntime.SettingsPreferenceSource
                : loginRuntime.SettingsPreferenceSourceFactory(authenticatedSession.UserId);
            var localizationDiagnostics = new ShellLocalizationDiagnosticsRecorder();
            var strings = HostStringCatalog.CreateResourceBacked(localizationDiagnostics.RecordMissingKey);
            var shellViewModel = ShellViewModel.CreateDefault(strings);
            var themeCoordinator = new ShellThemeCoordinator(scopedPreferenceSource);
            var currentTheme = themeCoordinator.CurrentTheme;
            shellViewModel.SetTheme(currentTheme);
            var preferences = scopedPreferenceSource.Load();
            if (preferences.IsSuccess)
            {
                shellViewModel.ApplyPreference(ShellPreferenceMapper.ToRuntimePreference(preferences.Value.ShellView));
            }

            var workSessionRegistry = new WorkSessionRegistry();
            var workSessionCloseCoordinator = new WorkSessionCloseCoordinator(workSessionRegistry);
            ConfigureSettingsAndAbout(shellViewModel, strings, loginRuntime, authenticatedSession?.UserId);
            WireBackgroundOperations(shellViewModel, loginRuntime.BackgroundOperations);
            ShellThemeCoordinator.ApplyTheme(this, currentTheme);
            var pluginPageRouteCoordinator = new PluginPageRouteCoordinator(
                loginRuntime.PluginRuntimeCoordinator.ExtensionPointRegistry,
                shellViewModel);

            var window = new MainWindow(
                loginRuntime.PostLoginSessionRuntime,
                authenticatedSession,
                loginRuntime.SelfServicePasswordChangeCoordinator,
                shellViewModel,
                loginRuntime.AccountSettingsModel,
                scopedPreferenceSource,
                themeCoordinator,
                loginRuntime.DiagnosticsExportCoordinator,
                loginRuntime.DiagnosticsSourceDirectory,
                loginRuntime.DiagnosticsOutputZipPath,
                workSessionCloseCoordinator,
                pluginPageRouteCoordinator: pluginPageRouteCoordinator,
                licenseImportCoordinator: loginRuntime.LicenseImportCoordinator,
                licenseFilePicker: new WindowsLicenseFilePicker());
            window.ContentRendered += async (_, _) =>
            {
                var activation = await loginRuntime.PluginRuntimeCoordinator.BeginPostLoginActivation(
                    new AssemblyHostVersionProvider(typeof(App).Assembly).Current,
                    authenticatedSession?.UserId ?? "unknown");
                if (activation.IsSuccess && activation.Value.Completion == PluginActivationCompletion.Completed)
                {
                    shellViewModel.ApplyPluginNavigationContributions(
                        PluginNavigationContributionMapper.Map(activation.Value.Run.NavigationContributions));
                }
            };
            MainWindow = window;
            window.ShowDialog();
            if (!window.ReturnToLoginRequested)
            {
                Shutdown();
                return;
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        singleInstanceCoordinator?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureSettingsAndAbout(
        ShellViewModel shellViewModel,
        HostStringCatalog strings,
        HostLoginRuntime loginRuntime,
        string? userId)
    {
        var actor = string.IsNullOrWhiteSpace(userId) ? "unknown" : userId;
        var categories = loginRuntime.SettingsNavigationModel
            .Build(actor!, Dobby.Core.CorrelationId.New())
            .Select(category => new ShellSettingsCategoryItem(
                category.Key,
                strings.GetString(category.LabelKey),
                category.IsAvailable))
            .ToArray();
        var permissionState = string.IsNullOrWhiteSpace(userId)
            ? null
            : loginRuntime.UserPermissionStateReader(userId);
        var isOwner = permissionState is { IsSuccess: true } && permissionState.Value.IsOwner;
        var about = loginRuntime.AboutInfoProvider.GetInfo(isOwner: isOwner, ownerIdentity: actor);
        shellViewModel.SetSettingsAndAbout(categories, BuildAboutFields(strings, about));
        ConfigureLicenseSurface(shellViewModel, loginRuntime, actor);
    }

    private static void ConfigureLicenseSurface(
        ShellViewModel shellViewModel,
        HostLoginRuntime loginRuntime,
        string actor)
    {
        var correlationId = Dobby.Core.CorrelationId.New();
        var canView = IsAllowed(loginRuntime.OperationAuthorizationPolicy, actor, OperationVerb.View, correlationId);
        var canImport = IsAllowed(loginRuntime.OperationAuthorizationPolicy, actor, OperationVerb.Import, correlationId);
        var licenseSurface = CurrentPluginLicenseSurface(loginRuntime);
        shellViewModel.SetLicenseSurface(
            canView,
            canImport,
            licenseSurface.Mode,
            pluginId: licenseSurface.PluginId,
            pluginName: licenseSurface.PluginName);
    }

    private static bool IsAllowed(
        OperationAuthorizationPolicy authorizationPolicy,
        string actor,
        OperationVerb verb,
        Dobby.Core.CorrelationId correlationId)
    {
        var decision = authorizationPolicy.Evaluate(
            new OperationPermissionCapability(
                new OperationKey(BuiltInOperationTargets.PluginLicenseTargetId, verb).ToString()),
            new PolicyContext(actor, null, correlationId));
        return decision is { IsSuccess: true, Value.Allowed: true };
    }

    private static PluginRuntimeMode CurrentPluginRuntimeMode(HostLoginRuntime loginRuntime)
    {
        return CurrentPluginLicenseSurface(loginRuntime).Mode;
    }

    private static LicenseSurfaceSnapshot CurrentPluginLicenseSurface(HostLoginRuntime loginRuntime)
    {
        var snapshot = loginRuntime.PluginRuntimeCoordinator.StateStore.Snapshot();
        var installed = snapshot is { IsSuccess: true } ? snapshot.Value.FirstOrDefault() : null;
        if (installed is null)
        {
            return new LicenseSurfaceSnapshot(PluginRuntimeMode.Denied, null, null);
        }

        var mode = loginRuntime.LicenseRuntimeModeProvider.GetCurrentMode(installed.PluginId, installed.PackageFingerprint);
        return new LicenseSurfaceSnapshot(
            mode is { IsSuccess: true } ? mode.Value : PluginRuntimeMode.Denied,
            installed.PluginId,
            installed.DisplayName);
    }

    private sealed record LicenseSurfaceSnapshot(
        PluginRuntimeMode Mode,
        string? PluginId,
        string? PluginName);

    private static IReadOnlyList<ShellAboutField> BuildAboutFields(HostStringCatalog strings, AboutInfo about)
    {
        var pluginText = about.Plugins.Count == 0
            ? strings.GetString(HostResourceKeys.AboutPluginsEmpty)
            : string.Join(", ", about.Plugins.Select(plugin => $"{plugin.PluginId} {plugin.Version} {plugin.Status}"));
        var ownerText = about.OwnerIdentityText.Equals(HostResourceKeys.AboutOwnerStatusRegistered, StringComparison.Ordinal)
            ? strings.GetString(HostResourceKeys.AboutOwnerStatusRegistered)
            : about.OwnerIdentityText;

        return
        [
            new ShellAboutField("app-version", strings.GetString(HostResourceKeys.AboutAppVersion), about.AppVersion),
            new ShellAboutField("build-number", strings.GetString(HostResourceKeys.AboutBuildNumber), about.BuildNumber),
            new ShellAboutField("build-date", strings.GetString(HostResourceKeys.AboutBuildDate), about.BuildDate),
            new ShellAboutField("host-edition", strings.GetString(HostResourceKeys.AboutHostEdition), about.HostEdition),
            new ShellAboutField("host-channel", strings.GetString(HostResourceKeys.AboutHostChannel), about.HostChannel),
            new ShellAboutField("device-code", strings.GetString(HostResourceKeys.AboutDeviceCode), about.DeviceCode),
            new ShellAboutField("plugins", strings.GetString(HostResourceKeys.AboutPlugins), pluginText),
            new ShellAboutField("support-contact", strings.GetString(HostResourceKeys.AboutSupportContact), about.SupportContact),
            new ShellAboutField("legal-line", strings.GetString(HostResourceKeys.AboutLegalLine), about.LegalLine),
            new ShellAboutField("owner-identity", strings.GetString(HostResourceKeys.AboutOwnerIdentity), ownerText)
        ];
    }

    private void WireBackgroundOperations(ShellViewModel shellViewModel, Dobby.Contracts.BackgroundOperations.IBackgroundOperationReporter reporter)
    {
        var coordinator = new ShellBackgroundOperationCoordinator(
            shellViewModel,
            reporter,
            new WpfUiDispatcher(Dispatcher));
        coordinator.Attach();
    }
}

using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.Policies;
using Dobby.Core.Versioning;
using Dobby.Configuration;
using Dobby.Diagnostics;
using Dobby.Host.Login;
using Dobby.Host.Plugins;
using Dobby.Host.Settings;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.Security.Authentication;
using Dobby.Security.Authorization;
using Dobby.Shell;
using Dobby.Shell.Layout;
using Dobby.Shell.UiDispatch;
using WpfApplication = System.Windows.Application;

namespace Dobby.Host;

internal static class PostLoginShellPresenter
{
    public static async Task<bool> ShowAsync(
        WpfApplication application,
        PreparedLoginRuntime preparedLoginRuntime,
        SessionRecord? authenticatedSession,
        AppThemePreference startupTheme,
        PostLoginTimingTrace? postLoginTiming = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(preparedLoginRuntime);

        await using var preparedRuntimeLease = preparedLoginRuntime.ConfigureAwait(false);
        var loginRuntime = preparedLoginRuntime.LoginRuntime;
        postLoginTiming?.MarkShellCompositionStarted();
        var scopedPreferenceSource = authenticatedSession is null
            ? loginRuntime.SettingsPreferenceSource
            : loginRuntime.SettingsPreferenceSourceFactory(authenticatedSession.UserId);
        var localizationDiagnostics = new ShellLocalizationDiagnosticsRecorder();
        var strings = HostStringCatalog.CreateResourceBacked(localizationDiagnostics.RecordMissingKey);
        var shellViewModel = ShellViewModel.CreateDefault(strings);
        var themeCoordinator = new ShellThemeCoordinator(scopedPreferenceSource);
        var currentTheme = startupTheme;
        shellViewModel.SetTheme(currentTheme);

        WireBackgroundOperations(application, shellViewModel, loginRuntime.BackgroundOperations);
        var window = new MainWindow(shellViewModel);
        postLoginTiming?.MarkShellWindowConstructed();
        window.AttachRuntime(
            loginRuntime,
            authenticatedSession,
            scopedPreferenceSource,
            themeCoordinator,
            null);
        postLoginTiming?.MarkShellRuntimeAttached();
        window.ContentRendered += (_, _) =>
        {
            postLoginTiming?.MarkShellFirstPaint();
            if (postLoginTiming is not null)
            {
                _ = postLoginTiming.FlushAfterShellFirstPaintAsync();
            }

            _ = RunPostLoginShellWarmupAsync(
                application,
                window,
                shellViewModel,
                strings,
                scopedPreferenceSource,
                currentTheme,
                loginRuntime,
                authenticatedSession?.UserId);
        };
        application.MainWindow = window;
        window.MaximizeForShellStartup();
        window.ShowDialog();
        return window.ReturnToLoginRequested;
    }

    private static async Task RunPostLoginShellWarmupAsync(
        WpfApplication application,
        MainWindow window,
        ShellViewModel shellViewModel,
        HostStringCatalog strings,
        ISettingsPreferenceSource scopedPreferenceSource,
        AppThemePreference currentTheme,
        HostLoginRuntime loginRuntime,
        string? userId)
    {
        var actor = string.IsNullOrWhiteSpace(userId) ? "unknown" : userId;
        try
        {
            var preferenceTask = Task.Run(() => scopedPreferenceSource.Load());
            var settingsAndAboutTask = Task.Run(() => BuildSettingsAndAbout(strings, loginRuntime, userId));
            var pluginMigrationTask = Task.Run(() => loginRuntime.PluginRuntimeCoordinator.ApplyPostLoginMigrations());

            var preferences = await preferenceTask.ConfigureAwait(false);
            if (preferences.IsSuccess)
            {
                currentTheme = preferences.Value.Appearance.Theme;
                var runtimePreference = ShellPreferenceMapper.ToRuntimePreference(preferences.Value.ShellView);
                await InvokeIfWindowAliveAsync(window, () =>
                {
                    shellViewModel.SetTheme(currentTheme);
                    ShellThemeCoordinator.ApplyTheme(application, currentTheme);
                    window.ApplyRuntimeShellPreference(runtimePreference);
                }).ConfigureAwait(false);
            }

            _ = Task.Run(() => StartupThemeSnapshotStore.SaveTheme(currentTheme));
            var pluginMigration = await pluginMigrationTask.ConfigureAwait(false);

            var settingsAndAbout = await settingsAndAboutTask.ConfigureAwait(false);
            await InvokeIfWindowAliveAsync(
                window,
                () => shellViewModel.SetSettingsAndAbout(settingsAndAbout.Categories, settingsAndAbout.AboutFields))
                .ConfigureAwait(false);

            if (!pluginMigration.IsSuccess)
            {
                await InvokeIfWindowAliveAsync(
                    window,
                    () => shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.ShellFeedbackError))
                    .ConfigureAwait(false);
                return;
            }

            var pluginsTask = Task.Run(() => BuildPluginsManagerSurface(loginRuntime, userId));
            var licenseTask = Task.Run(() => BuildLicenseSurface(loginRuntime, actor));
            var plugins = await pluginsTask.ConfigureAwait(false);
            var license = await licenseTask.ConfigureAwait(false);
            await InvokeIfWindowAliveAsync(window, () =>
            {
                shellViewModel.SetPluginsManagerSurface(
                    plugins.CanView,
                    plugins.CanImport,
                    plugins.CanConfigure,
                    plugins.CanDelete,
                    plugins.Items);
                shellViewModel.SetLicenseSurface(
                    license.CanView,
                    license.CanImport,
                    license.Mode,
                    license.PluginId,
                    license.PluginName);
            }).ConfigureAwait(false);

            var activation = await loginRuntime.PluginRuntimeCoordinator.BeginPostLoginActivation(
                new AssemblyHostVersionProvider(typeof(PostLoginShellPresenter).Assembly).Current,
                actor).ConfigureAwait(false);
            if (activation.IsSuccess && activation.Value.Completion == PluginActivationCompletion.Completed)
            {
                var contributions = PluginNavigationContributionMapper.Map(activation.Value.Run.NavigationContributions);
                await InvokeIfWindowAliveAsync(
                    window,
                    () => shellViewModel.ApplyPluginNavigationContributions(contributions))
                    .ConfigureAwait(false);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (InvalidOperationException)
        {
            await InvokeIfWindowAliveAsync(
                window,
                () => shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.ShellFeedbackError))
                .ConfigureAwait(false);
        }
    }

    private static async Task InvokeIfWindowAliveAsync(MainWindow window, Action action)
    {
        if (window.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        try
        {
            await window.Dispatcher.InvokeAsync(() =>
            {
                if (window.IsVisible)
                {
                    action();
                }
            }).Task.ConfigureAwait(false);
        }
        catch (InvalidOperationException) when (window.Dispatcher.HasShutdownStarted)
        {
        }
        catch (TaskCanceledException)
        {
        }
    }

    private static SettingsAndAboutSnapshot BuildSettingsAndAbout(
        HostStringCatalog strings,
        HostLoginRuntime loginRuntime,
        string? userId)
    {
        var actor = string.IsNullOrWhiteSpace(userId) ? "unknown" : userId;
        var categories = loginRuntime.SettingsNavigationModel
            .Build(actor, Dobby.Core.CorrelationId.New())
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
        return new SettingsAndAboutSnapshot(categories, BuildAboutFields(strings, about));
    }

    private static LicenseSurfaceConfiguration BuildLicenseSurface(
        HostLoginRuntime loginRuntime,
        string actor)
    {
        var correlationId = Dobby.Core.CorrelationId.New();
        var canView = IsAllowed(loginRuntime.OperationAuthorizationPolicy, actor, OperationVerb.View, correlationId);
        var canImport = IsAllowed(loginRuntime.OperationAuthorizationPolicy, actor, OperationVerb.Import, correlationId);
        var licenseSurface = CurrentPluginLicenseSurface(loginRuntime);
        return new LicenseSurfaceConfiguration(
            canView,
            canImport,
            licenseSurface.Mode,
            licenseSurface.PluginId,
            licenseSurface.PluginName);
    }

    private static PluginsManagerSurfaceConfiguration BuildPluginsManagerSurface(
        HostLoginRuntime loginRuntime,
        string? userId)
    {
        var actor = string.IsNullOrWhiteSpace(userId) ? "unknown" : userId;
        var correlationId = Dobby.Core.CorrelationId.New();
        var canView = IsAllowed(
            loginRuntime.OperationAuthorizationPolicy,
            actor,
            BuiltInOperationTargets.PluginManagementTargetId,
            OperationVerb.View,
            correlationId);
        var canImport = IsAllowed(
            loginRuntime.OperationAuthorizationPolicy,
            actor,
            BuiltInOperationTargets.PluginManagementTargetId,
            OperationVerb.Import,
            correlationId);
        var canConfigure = IsAllowed(
            loginRuntime.OperationAuthorizationPolicy,
            actor,
            BuiltInOperationTargets.PluginManagementTargetId,
            OperationVerb.Configure,
            correlationId);
        var canDelete = IsAllowed(
            loginRuntime.OperationAuthorizationPolicy,
            actor,
            BuiltInOperationTargets.PluginManagementTargetId,
            OperationVerb.Delete,
            correlationId);
        var snapshot = loginRuntime.PluginRuntimeCoordinator.MetadataCache.Snapshot();
        return new PluginsManagerSurfaceConfiguration(
            canView,
            canImport,
            canConfigure,
            canDelete,
            snapshot is { IsSuccess: true }
                ? snapshot.Value.Select(plugin => new ShellPluginManagerItem(
                    plugin.PluginId,
                    plugin.DisplayName,
                    plugin.Version,
                    plugin.Description,
                    plugin.IsEnabled,
                    plugin.StatusSummary,
                    plugin.IsPendingUninstall,
                    plugin.RestartRequired)).ToArray()
                : []);
    }

    private static bool IsAllowed(
        OperationAuthorizationPolicy authorizationPolicy,
        string actor,
        OperationVerb verb,
        Dobby.Core.CorrelationId correlationId)
    {
        return IsAllowed(
            authorizationPolicy,
            actor,
            BuiltInOperationTargets.PluginLicenseTargetId,
            verb,
            correlationId);
    }

    private static bool IsAllowed(
        OperationAuthorizationPolicy authorizationPolicy,
        string actor,
        string targetId,
        OperationVerb verb,
        Dobby.Core.CorrelationId correlationId)
    {
        var decision = authorizationPolicy.Evaluate(
            new OperationPermissionCapability(
                new OperationKey(targetId, verb).ToString()),
            new PolicyContext(actor, null, correlationId));
        return decision is { IsSuccess: true, Value.Allowed: true };
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

    private static void WireBackgroundOperations(
        WpfApplication application,
        ShellViewModel shellViewModel,
        IBackgroundOperationReporter reporter)
    {
        var coordinator = new ShellBackgroundOperationCoordinator(
            shellViewModel,
            reporter,
            new WpfUiDispatcher(application.Dispatcher));
        coordinator.Attach();
    }

    private sealed record LicenseSurfaceSnapshot(
        PluginRuntimeMode Mode,
        string? PluginId,
        string? PluginName);

    private sealed record SettingsAndAboutSnapshot(
        IReadOnlyList<ShellSettingsCategoryItem> Categories,
        IReadOnlyList<ShellAboutField> AboutFields);

    private sealed record LicenseSurfaceConfiguration(
        bool CanView,
        bool CanImport,
        PluginRuntimeMode Mode,
        string? PluginId,
        string? PluginName);

    private sealed record PluginsManagerSurfaceConfiguration(
        bool CanView,
        bool CanImport,
        bool CanConfigure,
        bool CanDelete,
        IReadOnlyList<ShellPluginManagerItem> Items);
}

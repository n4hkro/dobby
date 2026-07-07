using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Contracts.Ordering;
using Dobby.IntegrationTests.Shell;
using Dobby.Host;
using Dobby.Host.Plugins;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authentication;
using Dobby.Security.Authorization;
using Dobby.Shell;
using Dobby.Shell.WorkSessions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Dobby.IntegrationTests.Host;

[Collection(WpfIntegrationTestCollection.Name)]
public sealed class WorkSessionShellIntegrationTests
{
    [Fact]
    public void Lock_preserves_sessions_logout_can_cancel_or_close_and_restore_retains_state()
    {
        var registry = new WorkSessionRegistry();
        var closeCoordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        registry.UpdateProgress(session.SessionId, WorkSessionProgress.Determinate(80));
        registry.RecordInputState(session.SessionId, "query", "Şəki");
        registry.RecordScreenState(session.SessionId, "selected-tab", "results");

        closeCoordinator.HandleLock();
        registry.Get(session.SessionId).ShouldNotBeNull();

        var logout = closeCoordinator.RequestClose(session.SessionId, WorkSessionCloseReason.Logout);
        closeCoordinator.ApplyChoice(logout, WorkSessionCloseChoice.Cancel).IsSuccess.ShouldBeTrue();
        registry.Get(session.SessionId).ShouldNotBeNull();

        registry.Minimize(session.SessionId);
        registry.Restore(session.SessionId);
        var restored = registry.Get(session.SessionId)!;
        restored.Progress.Percent.ShouldBe(80);
        restored.InputState["query"].ShouldBe("Şəki");
        restored.ScreenState["selected-tab"].ShouldBe("results");

        closeCoordinator.ApplyChoice(logout, WorkSessionCloseChoice.Close).IsSuccess.ShouldBeTrue();
        registry.Get(session.SessionId).ShouldBeNull();
    }

    [Fact]
    public void Route_button_click_updates_navigation_without_removed_work_session_warning_surface()
    {
        RunOnSta(() =>
        {
            var registry = new WorkSessionRegistry();
            var closeCoordinator = new WorkSessionCloseCoordinator(registry);
            var shell = new ShellViewModel(
                HostStringCatalog.CreateResourceBacked(),
                SnapshotContributions());
            var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
            MainWindow? window = null;

            try
            {
                window = new MainWindow(shell)
                {
                    Left = -32000,
                    Top = -32000,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize
                };
                AddThemeResources(window);
                window.Show();
                Pump(window);

                FindNamedOrDefault<FrameworkElement>(window, "WorkSessionWarningPanel").ShouldBeNull();
                FindNamedOrDefault<Button>(window, "WorkSessionStayButton").ShouldBeNull();
                FindNamedOrDefault<Button>(window, "WorkSessionMinimizeButton").ShouldBeNull();
                FindNamedOrDefault<Button>(window, "WorkSessionCloseButton").ShouldBeNull();

                ClickRoute(window, "plugin.sample.analytics");
                shell.ActiveRouteKey.ShouldBe("plugin.sample.analytics");
                shell.HasPendingWorkSessionCloseRequest.ShouldBeFalse();
                registry.Get(session.SessionId).ShouldNotBeNull();
            }
            finally
            {
                window?.Close();
            }
        });
    }

    [Fact]
    public void Removed_logout_surface_does_not_render_work_session_warning_or_close_sessions()
    {
        RunOnSta(() =>
        {
            var registry = new WorkSessionRegistry();
            var closeCoordinator = new WorkSessionCloseCoordinator(registry);
            var shell = new ShellViewModel(
                HostStringCatalog.CreateResourceBacked(),
                SnapshotContributions());
            var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
            MainWindow? window = null;

            try
            {
                window = new MainWindow(shell)
                {
                    Left = -32000,
                    Top = -32000,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize
                };
                AddThemeResources(window);
                window.Show();
                Pump(window);

                FindNamedOrDefault<Button>(window, "LogoutButton").ShouldBeNull();
                FindNamedOrDefault<FrameworkElement>(window, "WorkSessionWarningPanel").ShouldBeNull();
                FindNamedOrDefault<Button>(window, "WorkSessionStayButton").ShouldBeNull();
                FindNamedOrDefault<Button>(window, "WorkSessionCloseButton").ShouldBeNull();
                window.ReturnToLoginRequested.ShouldBeFalse();
                registry.Get(session.SessionId).ShouldNotBeNull();
            }
            finally
            {
                if (window?.IsLoaded == true)
                {
                    window.Close();
                }
            }
        });
    }

    [Fact]
    public void Plugin_disable_request_uses_same_close_cancel_minimize_lifecycle_without_navigation()
    {
        var registry = new WorkSessionRegistry();
        var closeCoordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");

        var pending = closeCoordinator.RequestPluginDisable("plugin.sample.work");

        pending.ShouldNotBeNull();
        pending.Action.ShouldBe(PendingWorkSessionAction.PluginDisable);
        pending.RequestedRouteKey.ShouldBeNull();
        pending.CloseRequest.Reason.ShouldBe(WorkSessionCloseReason.PluginDisable);
        closeCoordinator.ApplyChoice(pending.CloseRequest, WorkSessionCloseChoice.Cancel).IsSuccess.ShouldBeTrue();
        registry.Get(session.SessionId).ShouldNotBeNull();

        pending = closeCoordinator.RequestPluginDisable("plugin.sample.work");
        pending.ShouldNotBeNull();
        closeCoordinator.ApplyChoice(pending.CloseRequest, WorkSessionCloseChoice.Minimize).IsSuccess.ShouldBeTrue();
        registry.Get(session.SessionId)!.IsMinimized.ShouldBeTrue();
    }

    [Fact]
    public void Removed_plugins_manager_disable_surface_does_not_mutate_active_plugin_work_session()
    {
        RunOnSta(() =>
        {
            const string pluginId = "com.dobby.sample";
            var registry = new WorkSessionRegistry();
            var closeCoordinator = new WorkSessionCloseCoordinator(registry);
            var activeSession = registry.Open(pluginId, "plugin/com.dobby.sample/greet", "Sample action");
            var shell = CreatePluginManagerShell(pluginId);
            var stateStore = Substitute.For<IInstalledPluginStateStore>();
            var metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
            var state = CreatePluginState(pluginId, isEnabled: true);
            var disabled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            stateStore.Get(pluginId).Returns(Result.Success<InstalledPluginState?>(state));
            stateStore.SetEnabled(pluginId, false, "Disabled").Returns(_ =>
            {
                disabled.TrySetResult(true);
                return Result.Success();
            });
            metadataCache.Upsert(Arg.Any<InstalledPluginMetadata>()).Returns(Result.Success());
            metadataCache.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginMetadata>>(
            [
                CreatePluginMetadata(pluginId, isEnabled: false, "Disabled")
            ]));
            var lifecycle = CreateLifecycleCoordinator(stateStore, metadataCache);
            MainWindow? window = null;

            try
            {
                window = CreatePluginManagerWindow(shell, closeCoordinator, lifecycle);
                window.Show();
                Pump(window);

                FindNamedOrDefault<Button>(window, "PluginsManagerDisableButton").ShouldBeNull();
                FindNamedOrDefault<FrameworkElement>(window, "WorkSessionWarningPanel").ShouldBeNull();
                stateStore.DidNotReceive().SetEnabled(pluginId, false, "Disabled");
                registry.Get(activeSession.SessionId).ShouldNotBeNull();
                disabled.Task.IsCompleted.ShouldBeFalse();
            }
            finally
            {
                window?.Close();
            }
        });
    }

    [Fact]
    public void Removed_plugins_manager_uninstall_surface_does_not_remove_active_plugin_work_session()
    {
        RunOnSta(() =>
        {
            const string pluginId = "com.dobby.sample";
            var registry = new WorkSessionRegistry();
            var closeCoordinator = new WorkSessionCloseCoordinator(registry);
            var activeSession = registry.Open(pluginId, "plugin/com.dobby.sample/greet", "Sample action");
            var shell = CreatePluginManagerShell(pluginId);
            var stateStore = Substitute.For<IInstalledPluginStateStore>();
            var metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
            var staging = Substitute.For<IPluginInstallStagingArea>();
            var dataRetention = Substitute.For<IPluginDataRetentionStore>();
            var activationTracker = Substitute.For<IPluginCurrentProcessActivationTracker>();
            var confirmation = Substitute.For<IPluginUninstallConfirmation>();
            var state = CreatePluginState(pluginId, isEnabled: true);
            var removed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            stateStore.Get(pluginId).Returns(Result.Success<InstalledPluginState?>(state));
            stateStore.Remove(pluginId).Returns(_ =>
            {
                removed.TrySetResult(true);
                return Result.Success();
            });
            metadataCache.Remove(pluginId).Returns(Result.Success());
            metadataCache.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginMetadata>>([]));
            staging.Cleanup(state.RuntimePath).Returns(Result.Success());
            dataRetention.DeletePluginData(pluginId).Returns(Result.Success());
            activationTracker.IsActivatedInCurrentProcess(pluginId).Returns(false);
            confirmation.Confirm(Arg.Any<PluginUninstallConfirmationRequest>())
                .Returns(PluginUninstallConfirmationResult.Confirm(PluginUninstallDataChoice.DeleteData));
            var lifecycle = CreateLifecycleCoordinator(
                stateStore,
                metadataCache,
                staging,
                dataRetention,
                activationTracker);
            MainWindow? window = null;

            try
            {
                window = CreatePluginManagerWindow(shell, closeCoordinator, lifecycle, confirmation);
                window.Show();
                Pump(window);

                FindNamedOrDefault<Button>(window, "PluginsManagerUninstallButton").ShouldBeNull();
                FindNamedOrDefault<FrameworkElement>(window, "WorkSessionWarningPanel").ShouldBeNull();
                stateStore.DidNotReceive().Remove(pluginId);
                registry.Get(activeSession.SessionId).ShouldNotBeNull();
                dataRetention.DidNotReceive().DeletePluginData(pluginId);
                removed.Task.IsCompleted.ShouldBeFalse();
            }
            finally
            {
                window?.Close();
            }
        });
    }

    private static IReadOnlyList<NavigationContribution> SnapshotContributions()
    {
        var parent = NavigationContribution.Create(
            "sample.analytics",
            "plugin.sample.analytics",
            "Dobby.Plugin.Sample.Analytics",
            NavigationTarget.TopToolbar,
            new UiOrderingKey("default", 50, "sample", 0));
        var child = NavigationContribution.Create(
            "sample.analytics.daily",
            "plugin.sample.analytics.daily",
            "Dobby.Plugin.Sample.Analytics.Daily",
            NavigationTarget.Sidebar,
            new UiOrderingKey("default", 10, "sample", 1),
            parent.Id);
        var leaf = NavigationContribution.Create(
            "sample.analytics.daily.filters",
            "plugin.sample.analytics.daily.filters",
            "Dobby.Plugin.Sample.Analytics.Daily.Filters",
            NavigationTarget.ThirdBar,
            new UiOrderingKey("default", 10, "sample", 2),
            child.Id);

        return [parent, child, leaf];
    }

    private static ShellViewModel CreatePluginManagerShell(string pluginId)
    {
        var shell = new ShellViewModel(
            HostStringCatalog.CreateResourceBacked(),
            SnapshotContributions());
        shell.SetPluginsManagerSurface(
            canView: true,
            canImport: false,
            canConfigure: true,
            canDelete: true,
            [
                new ShellPluginManagerItem(
                    pluginId,
                    "Sample plugin",
                    "1.0.0",
                    "Sample plugin",
                    IsEnabled: true,
                    "Enabled",
                    IsPendingUninstall: false,
                    RestartRequired: false)
            ]);
        return shell;
    }

    private static MainWindow CreatePluginManagerWindow(
        ShellViewModel shell,
        WorkSessionCloseCoordinator closeCoordinator,
        HostPluginLifecycleCoordinator lifecycle,
        IPluginUninstallConfirmation? uninstallConfirmation = null)
    {
        var now = DateTimeOffset.Parse("2026-06-20T00:00:00Z");
        var window = new MainWindow(
            shell,
            null,
            null,
            initialPreference: null,
            session: new SessionRecord("session-1", "owner", now, now, now.AddHours(8), null, 1))
        {
            Left = -32000,
            Top = -32000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize
        };
        AddThemeResources(window);
        return window;
    }

    private static HostPluginLifecycleCoordinator CreateLifecycleCoordinator(
        IInstalledPluginStateStore stateStore,
        IInstalledPluginMetadataCache metadataCache,
        IPluginInstallStagingArea? staging = null,
        IPluginDataRetentionStore? dataRetentionStore = null,
        IPluginCurrentProcessActivationTracker? activationTracker = null)
    {
        return new HostPluginLifecycleCoordinator(
            stateStore,
            metadataCache,
            staging ?? Substitute.For<IPluginInstallStagingArea>(),
            dataRetentionStore ?? Substitute.For<IPluginDataRetentionStore>(),
            activationTracker ?? Substitute.For<IPluginCurrentProcessActivationTracker>(),
            new OperationAuthorizationPolicy(
                OperationTargetRegistry.CreateDefault(),
                new StaticGrantReader(new UserPermissionState("owner", IsOwner: true, IsActive: true))),
            new PluginRuntimeAuditCoordinator(
                new InMemoryAuditTrail(),
                new FakeTimeProvider(DateTimeOffset.Parse("2026-06-20T00:00:00Z"))));
    }

    private static InstalledPluginState CreatePluginState(string pluginId, bool isEnabled)
    {
        var now = DateTimeOffset.Parse("2026-06-20T00:00:00Z");
        return new InstalledPluginState(
            pluginId,
            "1.0.0",
            "fingerprint",
            "Sample plugin",
            "Sample plugin",
            isEnabled,
            "verified",
            "runtime-path",
            isEnabled ? "Enabled" : "Disabled",
            now,
            now);
    }

    private static InstalledPluginMetadata CreatePluginMetadata(
        string pluginId,
        bool isEnabled,
        string statusSummary)
    {
        var now = DateTimeOffset.Parse("2026-06-20T00:00:00Z");
        return new InstalledPluginMetadata(
            pluginId,
            "Sample plugin",
            "1.0.0",
            "Sample plugin",
            "fingerprint",
            isEnabled,
            statusSummary,
            now,
            now,
            IsPendingUninstall: false,
            RestartRequired: false);
    }

    private static void AddThemeResources(Window window)
    {
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/Dobby.UI;component/Themes/DesignTokens.xaml", UriKind.Relative)
        });
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"/Dobby.UI;component/Themes/{AppThemePreference.Light}Theme.xaml", UriKind.Relative)
        });
    }

    private static void ClickRoute(Window window, string routeKey)
    {
        var button = FindVisuals<Button>((DependencyObject)window.Content)
            .Single(candidate => candidate.Tag is string tag && tag.Equals(routeKey, StringComparison.Ordinal));
        Click(button, window);
    }

    private static void ClickNamed(Window window, string name)
    {
        Click(FindNamed<Button>(window, name), window);
    }

    private static void Click(Button button, Window window)
    {
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        if (window.IsLoaded)
        {
            Pump(window);
        }
    }

    private static T FindNamed<T>(Window window, string name)
        where T : FrameworkElement
    {
        return FindVisuals<T>((DependencyObject)window.Content)
            .Single(element => element.Name.Equals(name, StringComparison.Ordinal));
    }

    private static T? FindNamedOrDefault<T>(Window window, string name)
        where T : FrameworkElement
    {
        return FindVisuals<T>((DependencyObject)window.Content)
            .SingleOrDefault(element => element.Name.Equals(name, StringComparison.Ordinal));
    }

    private static IReadOnlyList<T> FindVisuals<T>(DependencyObject root)
        where T : DependencyObject
    {
        var matches = new List<T>();
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                matches.Add(match);
            }

            matches.AddRange(FindVisuals<T>(child));
        }

        return matches;
    }

    private static void Pump(Window window)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private static void WaitForLifecycleAction(Window window, Task completion)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!completion.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            Pump(window);
            Thread.Sleep(10);
        }

        completion.IsCompleted.ShouldBeTrue();
        completion.GetAwaiter().GetResult();
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class StaticGrantReader(UserPermissionState state) : IPermissionGrantReader
    {
        public Result<UserPermissionState> LoadUserState(string userId)
        {
            return userId.Equals(state.UserId, StringComparison.Ordinal)
                ? Result.Success(state)
                : Result.Failure<UserPermissionState>(ErrorCode.NotFound);
        }

        public Result<bool> HasGrant(string userId, OperationKey key)
        {
            return Result.Success(false);
        }
    }
}

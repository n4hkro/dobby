using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Dobby.Configuration;
using Dobby.Contracts.Ordering;
using Dobby.Host;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell;
using Dobby.Shell.WorkSessions;
using Shouldly;

namespace Dobby.IntegrationTests.Host;

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
    public void Route_button_click_shows_warning_and_stay_minimize_close_resolve_navigation()
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
                window = new MainWindow(
                    null!,
                    null,
                    null!,
                    shell,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    closeCoordinator)
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

                ClickRoute(window, "plugin.sample.analytics");
                FindNamed<FrameworkElement>(window, "WorkSessionWarningPanel").Visibility.ShouldBe(Visibility.Visible);
                shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);

                FindNamed<Button>(window, "WorkSessionStayButton").Content.ShouldBe("Qal");
                ClickNamed(window, "WorkSessionStayButton");
                shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
                shell.HasPendingWorkSessionCloseRequest.ShouldBeFalse();
                FindNamed<FrameworkElement>(window, "WorkSessionWarningPanel").Visibility.ShouldBe(Visibility.Collapsed);

                ClickRoute(window, "plugin.sample.analytics");
                FindNamed<FrameworkElement>(window, "WorkSessionWarningPanel").Visibility.ShouldBe(Visibility.Visible);
                ClickNamed(window, "WorkSessionMinimizeButton");
                shell.ActiveRouteKey.ShouldBe("plugin.sample.analytics");
                registry.Get(session.SessionId)!.IsMinimized.ShouldBeTrue();
                FindNamed<FrameworkElement>(window, "WorkSessionWarningPanel").Visibility.ShouldBe(Visibility.Collapsed);

                registry.Restore(session.SessionId);
                shell.ActivateRoute(RouteRegistry.HomeRouteKey).IsSuccess.ShouldBeTrue();
                Pump(window);

                ClickRoute(window, "plugin.sample.analytics");
                FindNamed<FrameworkElement>(window, "WorkSessionWarningPanel").Visibility.ShouldBe(Visibility.Visible);
                ClickNamed(window, "WorkSessionCloseButton");
                shell.ActiveRouteKey.ShouldBe("plugin.sample.analytics");
                registry.Get(session.SessionId).ShouldBeNull();
                shell.HasPendingWorkSessionCloseRequest.ShouldBeFalse();
                FindNamed<FrameworkElement>(window, "WorkSessionWarningPanel").Visibility.ShouldBe(Visibility.Collapsed);
            }
            finally
            {
                window?.Close();
            }
        });
    }

    [Fact]
    public void Logout_button_uses_work_session_warning_and_cancel_or_close_before_returning_to_login()
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
                window = new MainWindow(
                    null!,
                    null,
                    null!,
                    shell,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    closeCoordinator)
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

                ClickNamed(window, "LogoutButton");
                FindNamed<FrameworkElement>(window, "WorkSessionWarningPanel").Visibility.ShouldBe(Visibility.Visible);
                window.ReturnToLoginRequested.ShouldBeFalse();
                registry.Get(session.SessionId).ShouldNotBeNull();

                ClickNamed(window, "WorkSessionStayButton");
                FindNamed<FrameworkElement>(window, "WorkSessionWarningPanel").Visibility.ShouldBe(Visibility.Collapsed);
                window.ReturnToLoginRequested.ShouldBeFalse();
                registry.Get(session.SessionId).ShouldNotBeNull();

                ClickNamed(window, "LogoutButton");
                FindNamed<FrameworkElement>(window, "WorkSessionWarningPanel").Visibility.ShouldBe(Visibility.Visible);
                ClickNamed(window, "WorkSessionCloseButton");

                window.ReturnToLoginRequested.ShouldBeTrue();
                registry.Get(session.SessionId).ShouldBeNull();
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
}

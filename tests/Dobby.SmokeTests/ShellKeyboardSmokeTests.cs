using Dobby.Configuration;
using Dobby.Contracts.Ordering;
using Dobby.Contracts.PluginPages;
using Dobby.Host;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell;
using Shouldly;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Dobby.SmokeTests;

public sealed class ShellKeyboardSmokeTests
{
    [Fact]
    public void Shell_view_model_supports_keyboard_route_sidebar_profile_and_plugin_page_smoke()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());

        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();
        shell.SidebarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily"]);
        shell.SidebarNavigation.Single().IsOpen.ShouldBeTrue();
        shell.ThirdBarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily.filters"]);

        shell.ToggleSidebarCollapsed();
        shell.Layout.Preference.SidebarCollapsed.ShouldBeTrue();

        shell.SetActivePluginPage(new PluginTextPageModel("plugin.page.ready"));
        shell.ActivePluginPageModel.ShouldNotBeNull();

        shell.ProfileMenuItems.Select(item => item.Key).ShouldContain("logout");
        shell.FocusTraversal.Select(item => item.Name).ShouldContain("ProfileMenu.Logout");
    }

    [Fact]
    public void Shell_regions_are_keyboard_reachable()
    {
        RunOnSta(() =>
        {
            var shell = new ShellViewModel(HostStringCatalog.CreateResourceBacked(), SnapshotContributions());
            var window = new MainWindow
            {
                DataContext = shell,
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                var profileButton = FindNamed<Button>(window, "ProfileMenuButton");
                AutomationProperties.GetName(profileButton).ShouldBe("Profil və hesab məlumatı");
                profileButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                FindNamed<FrameworkElement>(window, "ProfileMenu").IsVisible.ShouldBeTrue();
                FindNamed<Button>(window, "ChangePasswordButton").IsVisible.ShouldBeTrue();
                FindNamed<Button>(window, "ManualLockButton").IsVisible.ShouldBeTrue();
                FindNamed<Button>(window, "LogoutButton").IsVisible.ShouldBeTrue();
                FindNamed<Button>(window, "DiagnosticsExportButton").IsVisible.ShouldBeTrue();
                FindNamed<FrameworkElement>(window, "ShellNotificationHost").IsVisible.ShouldBeTrue();
                FindNamed<FrameworkElement>(window, "ShellFeedbackHost").IsVisible.ShouldBeTrue();
                FindNamed<FrameworkElement>(window, "MinimizedWorkSessionStrip").IsVisible.ShouldBeTrue();

                var buttons = FindVisuals<Button>((DependencyObject)window.Content)
                    .Where(button => button.IsVisible)
                    .ToArray();
                buttons.Length.ShouldBeGreaterThanOrEqualTo(12);
                buttons.ShouldAllBe(button => button.Focusable);

                var first = buttons[0];
                Keyboard.Focus(first).ShouldBe(first);
                first.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)).ShouldBeTrue();
                Keyboard.FocusedElement.ShouldNotBeNull();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Shell_profile_menu_buttons_keep_built_in_navigation_and_plugin_page_host_available()
    {
        RunOnSta(() =>
        {
            var shell = new ShellViewModel(HostStringCatalog.CreateResourceBacked(), SnapshotContributions());
            shell.SetActivePluginPage(new PluginTextPageModel("plugin.page.ready"));
            var window = new MainWindow
            {
                DataContext = shell,
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                ClickRoute(window, RouteRegistry.SettingsRouteKey);
                Pump(window);

                shell.ActiveRouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
                FindNamed<FrameworkElement>(window, "MainContent").IsVisible.ShouldBeTrue();
                FindNamed<ContentControl>(window, "PluginPageHost").IsVisible.ShouldBeTrue();

                FindNamed<Button>(window, "ProfileMenuButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);
                FindNamed<Button>(window, "ProfileAccountInfoButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                shell.ActiveRouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
            }
            finally
            {
                window.Close();
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

    private static void ClickRoute(Window window, string routeKey)
    {
        var button = FindVisuals<Button>((DependencyObject)window.Content)
            .First(button => string.Equals(button.Tag as string, routeKey, StringComparison.Ordinal));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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

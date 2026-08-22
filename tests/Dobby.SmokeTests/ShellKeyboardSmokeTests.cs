using Dobby.Configuration;
using Dobby.Contracts.Ordering;
using Dobby.Contracts.PluginPages;
using Dobby.Host;
using Dobby.Host.Login;
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
            var window = new MainWindow(shell)
            {
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
            shell.SetSettingsAndAbout(
                [],
                [new Dobby.Shell.Layout.ShellAboutField("device-code", "Cihaz kodu", "ABCD-EFGH")]);
            var window = new MainWindow(shell)
            {
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
                var copyDeviceCode = FindNamed<Button>(window, "AboutCopyDeviceCodeButton");
                copyDeviceCode.Visibility.ShouldBe(Visibility.Visible);
                copyDeviceCode.BringIntoView();
                Pump(window);
                AutomationProperties.GetName(copyDeviceCode).ShouldNotBeNullOrWhiteSpace();
                AutomationProperties.GetHelpText(copyDeviceCode).ShouldNotBeNullOrWhiteSpace();

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

    [Fact]
    public void Login_and_recovery_controls_expose_accessible_identity_and_keyboard_semantics()
    {
        RunOnSta(() =>
        {
            var login = new LoginWindow
            {
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                AddThemeResources(login);
                login.Show();
                Pump(login);
                ((FrameworkElement)login.Content).LayoutTransform = new ScaleTransform(2, 2);
                Pump(login);

                foreach (var automationId in new[]
                {
                    "Dobby.Host.Login.Username",
                    "Dobby.Host.Login.Password",
                    "Dobby.Host.Login.RememberMe",
                    "Dobby.Host.Login.Submit",
                    "Dobby.Host.Login.Recovery"
                })
                {
                    var element = FindAutomationId(login, automationId);
                    AutomationProperties.GetName(element).ShouldNotBeNullOrWhiteSpace();
                }

                FindAutomationId<Button>(login, "Dobby.Host.Login.Submit").IsDefault.ShouldBeTrue();
                var recoveryButton = FindAutomationId<Button>(login, "Dobby.Host.Login.Recovery");
                recoveryButton.BringIntoView();
                Pump(login);
                recoveryButton.IsVisible.ShouldBeTrue();
                recoveryButton.IsTabStop.ShouldBeTrue();
                AutomationProperties.GetLiveSetting(FindNamed<TextBlock>(login, "ThrottleCountdownText"))
                    .ShouldBe(AutomationLiveSetting.Polite);
                AutomationProperties.GetLiveSetting(FindNamed<TextBlock>(login, "ErrorText"))
                    .ShouldBe(AutomationLiveSetting.Assertive);
            }
            finally
            {
                login.Close();
            }

            var reset = new RecoveryPasswordResetDialog
            {
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };
            try
            {
                AddThemeResources(reset);
                reset.Show();
                Pump(reset);
                ((FrameworkElement)reset.Content).LayoutTransform = new ScaleTransform(2, 2);
                Pump(reset);

                foreach (var automationId in new[]
                {
                    "Dobby.Host.Recovery.NewPassword",
                    "Dobby.Host.Recovery.ConfirmPassword",
                    "Dobby.Host.Recovery.Confirm",
                    "Dobby.Host.Recovery.Cancel"
                })
                {
                    AutomationProperties.GetName(FindAutomationId(reset, automationId))
                        .ShouldNotBeNullOrWhiteSpace();
                }

                FindAutomationId<Button>(reset, "Dobby.Host.Recovery.Confirm").IsDefault.ShouldBeTrue();
                var cancelButton = FindAutomationId<Button>(reset, "Dobby.Host.Recovery.Cancel");
                cancelButton.IsCancel.ShouldBeTrue();
                cancelButton.BringIntoView();
                Pump(reset);
                Keyboard.Focus(cancelButton).ShouldBe(cancelButton);
                AutomationProperties.GetLiveSetting(FindNamed<TextBlock>(reset, "ValidationText"))
                    .ShouldBe(AutomationLiveSetting.Assertive);
            }
            finally
            {
                reset.Close();
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

    private static FrameworkElement FindAutomationId(Window window, string automationId)
    {
        return FindAutomationId<FrameworkElement>(window, automationId);
    }

    private static T FindAutomationId<T>(Window window, string automationId)
        where T : FrameworkElement
    {
        return FindVisuals<T>((DependencyObject)window.Content)
            .Single(element => AutomationProperties.GetAutomationId(element)
                .Equals(automationId, StringComparison.Ordinal));
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

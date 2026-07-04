using Dobby.Contracts.Ordering;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell;
using Dobby.Shell.Layout;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Shouldly;

namespace Dobby.SmokeTests;

public sealed class ShellKeyboardSmokeTests
{
    [Fact]
    public void Shell_view_model_supports_keyboard_route_theme_sidebar_and_profile_inventory_smoke()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());

        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();
        shell.SidebarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily"]);
        shell.ThirdBarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily.filters"]);

        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);

        shell.ToggleSidebarCollapsed();
        shell.Layout.Preference.SidebarCollapsed.ShouldBeTrue();

        shell.SetTheme(AppThemePreference.Dark);
        shell.Layout.Theme.ShouldBe(AppThemePreference.Dark);

        shell.ProfileMenuItems.Select(item => item.Key).ShouldBe([
            "profile-account-info",
            "change-password",
            "manual-lock",
            "logout"
        ]);
        shell.FocusTraversal.Select(item => item.Name).ShouldContain("TopToolbar.Home");
        shell.FocusTraversal.Select(item => item.Name).ShouldContain("ProfileMenu.Logout");
    }

    [Fact]
    public void Rendered_shell_supports_keyboard_traversal_focusable_actions_and_accessible_labels()
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

                var profileMenu = FindNamed<FrameworkElement>(window, "ProfileMenuPanel");
                profileMenu.Visibility.ShouldBe(Visibility.Visible);
                FindNamed<Button>(window, "LogoutButton").IsVisible.ShouldBeTrue();
                FindNamed<Button>(window, "ManualLockButton").IsVisible.ShouldBeTrue();

                FindNamed<ItemsControl>(window, "NotificationPresentationList").Items.Count.ShouldBe(3);
                FindNamed<ItemsControl>(window, "FeedbackStatePresentationList").Items.Count.ShouldBe(5);

                var buttons = FindVisuals<Button>((DependencyObject)window.Content)
                    .Where(button => button.IsVisible)
                    .ToArray();
                buttons.Length.ShouldBeGreaterThanOrEqualTo(8);
                buttons.ShouldAllBe(button => button.Focusable);
                foreach (var button in buttons)
                {
                    button.Content?.ToString().ShouldNotBeNullOrWhiteSpace();
                }

                var first = buttons[0];
                Keyboard.Focus(first).ShouldBe(first);
                first.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)).ShouldBeTrue();
                Keyboard.FocusedElement.ShouldNotBeNull();

                FindNamed<Button>(window, "LogoutButton").Content.ShouldBe("Çıxış");
                FindNamed<Button>(window, "ManualLockButton").Content.ShouldBe("Kilidlə");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Rendered_shell_navigation_buttons_switch_builtin_route_surfaces()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow
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

                FindNamed<FrameworkElement>(window, "HomePanel").Visibility.ShouldBe(Visibility.Visible);
                FindNamed<FrameworkElement>(window, "PluginsSurface").Visibility.ShouldBe(Visibility.Collapsed);
                FindNamed<FrameworkElement>(window, "SettingsSurface").Visibility.ShouldBe(Visibility.Collapsed);

                ClickRoute(window, RouteRegistry.SettingsRouteKey);
                Pump(window);

                FindNamed<FrameworkElement>(window, "HomePanel").Visibility.ShouldBe(Visibility.Collapsed);
                FindNamed<FrameworkElement>(window, "PluginsSurface").Visibility.ShouldBe(Visibility.Collapsed);
                FindNamed<FrameworkElement>(window, "SettingsSurface").Visibility.ShouldBe(Visibility.Visible);

                ClickRoute(window, RouteRegistry.PluginsRouteKey);
                Pump(window);

                FindNamed<FrameworkElement>(window, "HomePanel").Visibility.ShouldBe(Visibility.Collapsed);
                FindNamed<FrameworkElement>(window, "PluginsSurface").Visibility.ShouldBe(Visibility.Visible);
                FindNamed<FrameworkElement>(window, "SettingsSurface").Visibility.ShouldBe(Visibility.Collapsed);

                ClickRoute(window, RouteRegistry.HomeRouteKey);
                Pump(window);

                FindNamed<FrameworkElement>(window, "HomePanel").Visibility.ShouldBe(Visibility.Visible);
                FindNamed<FrameworkElement>(window, "PluginsSurface").Visibility.ShouldBe(Visibility.Collapsed);
                FindNamed<FrameworkElement>(window, "SettingsSurface").Visibility.ShouldBe(Visibility.Collapsed);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Rendered_shell_with_initial_preference_applies_window_placement_without_loaded_preference_reload()
    {
        RunOnSta(() =>
        {
            var source = new CountingPreferenceSource();
            var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
            var initialPreference = ShellLayoutPreference.Default with
            {
                WindowPlacement = new ShellWindowPlacement(1280, 720, 32, 48)
            };
            var window = new MainWindow(
                null!,
                null,
                null!,
                shell,
                null,
                source,
                null,
                null,
                null,
                null,
                initialPreference: initialPreference)
            {
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                source.LoadCount.ShouldBe(0);
                window.Width.ShouldBe(1280);
                window.Height.ShouldBe(720);
                window.Left.ShouldBe(32);
                window.Top.ShouldBe(48);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Rendered_shell_window_close_button_requests_confirmation_and_respects_cancel()
    {
        RunOnSta(() =>
        {
            var confirmation = new RecordingShellWindowCloseConfirmation(result: false);
            var window = new MainWindow(
                null!,
                null,
                null!,
                ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked()),
                null,
                null,
                null,
                null,
                null,
                null,
                shellWindowCloseConfirmation: confirmation)
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

                FindNamed<Button>(window, "WindowCloseButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                confirmation.CallCount.ShouldBe(1);
                window.IsVisible.ShouldBeTrue();
            }
            finally
            {
                if (window.IsVisible)
                {
                    window.Close();
                }
            }
        });
    }

    [Fact]
    public void Rendered_shell_navigation_does_not_synchronously_read_or_save_preferences_on_click()
    {
        RunOnSta(() =>
        {
            var source = new GuardedPreferenceSource();
            var window = new MainWindow(
                null!,
                null,
                null!,
                ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked()),
                null,
                source,
                null,
                null,
                null,
                null,
                initialPreference: ShellLayoutPreference.Default)
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

                source.AllowPreferenceAccess = false;
                ClickRoute(window, RouteRegistry.SettingsRouteKey);
                Pump(window);
                source.AllowPreferenceAccess = true;

                FindNamed<FrameworkElement>(window, "SettingsSurface").Visibility.ShouldBe(Visibility.Visible);
                source.SynchronousBlockedAccessCount.ShouldBe(0);
            }
            finally
            {
                source.AllowPreferenceAccess = true;
                window.Close();
            }
        });
    }

    [Fact]
    public void Rendered_shell_uses_chrome_sized_workspace_tabs_and_visible_caption_buttons()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow
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

                var chrome = WindowChrome.GetWindowChrome(window);
                chrome.ShouldNotBeNull();
                chrome.UseAeroCaptionButtons.ShouldBeFalse();
                chrome.CaptionHeight.ShouldBe(48);
                chrome.GlassFrameThickness.ShouldBe(new Thickness(0));

                var tabFrame = FindVisuals<Border>((DependencyObject)window.Content)
                    .First(border => border.Name.Equals("WorkspaceTabFrame", StringComparison.Ordinal));
                tabFrame.ActualWidth.ShouldBe(240);
                tabFrame.ActualHeight.ShouldBe(42);

                foreach (var buttonName in new[] { "WindowMinimizeButton", "WindowMaximizeRestoreButton", "WindowCloseButton" })
                {
                    var button = FindNamed<Button>(window, buttonName);
                    button.IsVisible.ShouldBeTrue();
                    button.ActualWidth.ShouldBe(46);
                    button.ActualHeight.ShouldBe(32);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Rendered_shell_shrinks_workspace_tabs_before_caption_buttons_can_overflow()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow
            {
                Left = -32000,
                Top = -32000,
                Width = 1024,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                var newTabButton = FindNamed<Button>(window, "WorkspaceNewTabButton");
                for (var index = 0; index < 9; index++)
                {
                    newTabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Pump(window);
                }

                var tabFrames = FindVisuals<Border>((DependencyObject)window.Content)
                    .Where(border => border.Name.Equals("WorkspaceTabFrame", StringComparison.Ordinal))
                    .ToArray();
                tabFrames.Length.ShouldBe(10);
                tabFrames.ShouldAllBe(frame => frame.ActualWidth < 240);

                var titleBar = FindNamed<Grid>(window, "WindowTitleBar");
                var captionButtons = FindNamed<StackPanel>(window, "WindowCaptionButtons");
                var captionBounds = captionButtons
                    .TransformToAncestor(titleBar)
                    .TransformBounds(new Rect(0, 0, captionButtons.ActualWidth, captionButtons.ActualHeight));
                captionBounds.Right.ShouldBeLessThanOrEqualTo(titleBar.ActualWidth + 0.5);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Rendered_shell_keeps_caption_buttons_visible_when_window_is_narrow()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow
            {
                Left = -32000,
                Top = -32000,
                Width = 420,
                MinWidth = 360,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                var newTabButton = FindNamed<Button>(window, "WorkspaceNewTabButton");
                for (var index = 0; index < 8; index++)
                {
                    newTabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Pump(window);
                }

                var titleBar = FindNamed<Grid>(window, "WindowTitleBar");
                var captionButtons = FindNamed<StackPanel>(window, "WindowCaptionButtons");
                var captionBounds = captionButtons
                    .TransformToAncestor(titleBar)
                    .TransformBounds(new Rect(0, 0, captionButtons.ActualWidth, captionButtons.ActualHeight));

                captionButtons.IsVisible.ShouldBeTrue();
                captionButtons.ActualWidth.ShouldBe(138);
                captionBounds.Left.ShouldBeGreaterThanOrEqualTo(0);
                captionBounds.Right.ShouldBeLessThanOrEqualTo(titleBar.ActualWidth + 0.5);

                foreach (var buttonName in new[] { "WindowMinimizeButton", "WindowMaximizeRestoreButton", "WindowCloseButton" })
                {
                    var button = FindNamed<Button>(window, buttonName);
                    button.IsVisible.ShouldBeTrue();
                    button.ActualWidth.ShouldBe(46);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Rendered_shell_new_tab_opens_home_and_navigation_updates_active_tab()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow
            {
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };
            var shell = (ShellViewModel)window.DataContext;

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                FindNamed<ItemsControl>(window, "WorkspaceTabStrip").Items.Count.ShouldBe(1);
                shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");

                FindNamed<Button>(window, "WorkspaceNewTabButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                FindNamed<ItemsControl>(window, "WorkspaceTabStrip").Items.Count.ShouldBe(2);
                shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
                shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");

                ClickRoute(window, RouteRegistry.SettingsRouteKey);
                Pump(window);

                shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
                shell.ActiveWorkspaceTab.Title.ShouldBe("Tənzimləmələr");
                FindNamed<FrameworkElement>(window, "SettingsSurface").Visibility.ShouldBe(Visibility.Visible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Rendered_shell_closing_active_tab_activates_left_tab()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow
            {
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };
            var shell = (ShellViewModel)window.DataContext;

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                ClickRoute(window, RouteRegistry.SettingsRouteKey);
                Pump(window);
                var leftTabId = shell.ActiveWorkspaceTab.TabId;

                FindNamed<Button>(window, "WorkspaceNewTabButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);
                ClickRoute(window, RouteRegistry.PluginsRouteKey);
                Pump(window);

                var closeButton = WorkspaceTabCloseButtons(window)
                    .SingleOrDefault(button => ButtonTagId(button) == shell.ActiveWorkspaceTab.TabId);
                closeButton.ShouldNotBeNull(DumpWorkspaceButtons(window));
                closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                shell.WorkspaceTabs.Count.ShouldBe(1);
                shell.ActiveWorkspaceTab.TabId.ShouldBe(leftTabId);
                shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
                FindNamed<FrameworkElement>(window, "SettingsSurface").Visibility.ShouldBe(Visibility.Visible);
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

    private static void ClickRoute(Window window, string routeKey)
    {
        var button = FindVisuals<Button>((DependencyObject)window.Content)
            .First(button => string.Equals(button.Tag as string, routeKey, StringComparison.Ordinal));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static IReadOnlyList<Button> WorkspaceTabButtons(Window window)
    {
        return FindVisuals<Button>((DependencyObject)window.Content)
            .Where(button => button.Tag is Guid && button.Content is string text && text.Length > 1)
            .ToArray();
    }

    private static IReadOnlyList<Button> WorkspaceTabCloseButtons(Window window)
    {
        return FindVisuals<Button>((DependencyObject)window.Content)
            .Where(button => ButtonTagId(button).HasValue &&
                string.Equals(
                    AutomationProperties.GetName(button),
                    ((ShellViewModel)window.DataContext).WorkspaceCloseTabLabel,
                    StringComparison.Ordinal))
            .ToArray();
    }

    private static Guid? ButtonTagId(Button button)
    {
        return button.Tag switch
        {
            Guid id => id,
            string text when Guid.TryParse(text, out var id) => id,
            _ => null
        };
    }

    private static string DumpWorkspaceButtons(Window window)
    {
        var shell = (ShellViewModel)window.DataContext;
        var tabs = string.Join(
            Environment.NewLine,
            shell.WorkspaceTabs.Select(tab => $"tab id={tab.TabId} title={tab.Title} active={tab.IsActive} route={tab.RouteKey}"));
        var buttons = string.Join(
            Environment.NewLine,
            FindVisuals<Button>((DependencyObject)window.Content)
                .Where(button => button.Tag is not null)
                .Select(button => $"button tag={button.Tag} tagType={button.Tag.GetType().FullName} content={button.Content}"));
        return $"{tabs}{Environment.NewLine}{buttons}";
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

    private sealed class CountingPreferenceSource : ISettingsPreferenceSource
    {
        public int LoadCount { get; private set; }

        public Result<SettingsPreferences> Load()
        {
            LoadCount++;
            return Result.Success(SettingsPreferences.Default);
        }

        public Result Save(SettingsPreferences preferences)
        {
            return Result.Success();
        }
    }

    private sealed class GuardedPreferenceSource : ISettingsPreferenceSource
    {
        private readonly int ownerThreadId = Environment.CurrentManagedThreadId;

        public bool AllowPreferenceAccess { get; set; } = true;

        public int SynchronousBlockedAccessCount { get; private set; }

        public Result<SettingsPreferences> Load()
        {
            if (!AllowPreferenceAccess)
            {
                if (Environment.CurrentManagedThreadId == ownerThreadId)
                {
                    SynchronousBlockedAccessCount++;
                }

                throw new InvalidOperationException("Navigation click must not synchronously load preferences.");
            }

            return Result.Success(SettingsPreferences.Default);
        }

        public Result Save(SettingsPreferences preferences)
        {
            if (!AllowPreferenceAccess)
            {
                if (Environment.CurrentManagedThreadId == ownerThreadId)
                {
                    SynchronousBlockedAccessCount++;
                }

                throw new InvalidOperationException("Navigation click must not synchronously save preferences.");
            }

            return Result.Success();
        }
    }

    private sealed class RecordingShellWindowCloseConfirmation(bool result) : IShellWindowCloseConfirmation
    {
        public int CallCount { get; private set; }

        public bool ConfirmClose()
        {
            CallCount++;
            return result;
        }
    }
}

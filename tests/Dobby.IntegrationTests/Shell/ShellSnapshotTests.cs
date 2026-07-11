using System.Runtime.ExceptionServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dobby.Configuration;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Ordering;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Host;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell;
using Dobby.Shell.Layout;
using Shouldly;

namespace Dobby.IntegrationTests.Shell;

[Collection(WpfIntegrationTestCollection.Name)]
public sealed class ShellSnapshotTests
{
    [Fact]
    public void Main_shell_xaml_lists_required_named_regions()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot().FullName, "src", "Dobby.Host", "MainWindow.xaml"));

        foreach (var name in RequiredSurfaces())
        {
            xaml.ShouldContain($"x:Name=\"{name}\"");
        }
    }

    [Theory]
    [InlineData(1024, 680, AppThemePreference.Light)]
    [InlineData(1920, 1080, AppThemePreference.Dark)]
    public void Main_shell_renders_required_named_regions(
        int width,
        int height,
        AppThemePreference theme)
    {
        var evidence = RunOnSta(() => RenderShellEvidence(width, height, theme));

        foreach (var name in RequiredSurfaces())
        {
            evidence.SurfaceNames.ShouldContain(name);
        }

        evidence.RouteTags.ShouldContain(RouteRegistry.HomeRouteKey);
        evidence.RouteTags.ShouldContain(RouteRegistry.PluginsRouteKey);
        evidence.RouteTags.ShouldContain(RouteRegistry.SettingsRouteKey);
        evidence.Text.ShouldContain("Dobby");
        evidence.Text.ShouldContain("Ana səhifə");
        evidence.Text.ShouldContain("Pluginlər");
        evidence.Text.ShouldContain("Tənzimləmələr");
        evidence.Text.ShouldContain("Hazırdır");
        evidence.Text.ShouldContain("Yığılmış işlər");
        evidence.Text.ShouldContain("Qısa bildiriş");
        evidence.Text.ShouldContain("Yüklənir");
        evidence.Text.ShouldContain("Şifrəni dəyiş");
        evidence.UniqueSampledColors.ShouldBeGreaterThan(3);
    }

    [Fact]
    public void Main_content_renders_settings_about_and_plugin_page_surfaces()
    {
        var strings = HostStringCatalog.CreateResourceBacked();
        var settingsEvidence = RunOnSta(() => RenderShellEvidence(
            1366,
            768,
            AppThemePreference.Light,
            RouteRegistry.SettingsRouteKey));

        settingsEvidence.SurfaceNames.ShouldContain("MainContent");
        settingsEvidence.SurfaceNames.ShouldContain("AppearanceSettingsSurface");
        settingsEvidence.SurfaceNames.ShouldContain("AppearanceThemeSelector");
        settingsEvidence.SurfaceNames.ShouldContain("AccountSettingsSurface");
        settingsEvidence.SurfaceNames.ShouldContain("SettingsAccountProfileText");
        settingsEvidence.SurfaceNames.ShouldContain("SettingsNewPasswordBox");
        settingsEvidence.SurfaceNames.ShouldContain("SecuritySessionSettingsSurface");
        settingsEvidence.SurfaceNames.ShouldContain("SecurityAutoLockEnabledCheckBox");
        settingsEvidence.SurfaceNames.ShouldContain("SecurityAutoLockMinutesInput");
        settingsEvidence.SurfaceNames.ShouldContain("UserManagementSettingsSurface");
        settingsEvidence.SurfaceNames.ShouldContain("UserManagementActionList");
        settingsEvidence.SurfaceNames.ShouldContain("AboutSettingsSurface");
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.ShellSettingsHeading));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.SettingsAppearance));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.SettingsAccount));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.SettingsSecuritySession));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.SettingsUserManagement));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.SettingsAbout));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutAppVersion));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutBuildNumber));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutBuildDate));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutHostEdition));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutHostChannel));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutDeviceCode));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutPlugins));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutSupportContact));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutLegalLine));
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutOwnerIdentity));
        settingsEvidence.Text.ShouldContain("100");
        settingsEvidence.Text.ShouldContain("2026-07-09");
        settingsEvidence.Text.ShouldContain("local");
        settingsEvidence.Text.ShouldContain("stable");
        settingsEvidence.Text.ShouldContain("device-redacted-1234");
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutPluginsEmpty));
        settingsEvidence.Text.ShouldContain("support@example.invalid");
        settingsEvidence.Text.ShouldContain("Copyright Dobby");
        settingsEvidence.Text.ShouldContain(strings.GetString(HostResourceKeys.AboutOwnerStatusManaged));
        settingsEvidence.Text.ShouldNotContain(strings.GetString(HostResourceKeys.LicenseStatusLabel));
        settingsEvidence.Text.ShouldNotContain(strings.GetString(HostResourceKeys.LicenseImportAction));

        var pluginEvidence = RunOnSta(() => RenderShellEvidence(
            1366,
            768,
            AppThemePreference.Light,
            RouteRegistry.SettingsRouteKey,
            new PluginTextPageModel("plugin.page.ready")));

        pluginEvidence.SurfaceNames.ShouldContain("PluginPageHost");
        pluginEvidence.Text.ShouldContain("plugin.page.ready");
    }

    [Fact]
    public void Plugin_page_host_does_not_echo_unapproved_plugin_model_text()
    {
        var evidence = RunOnSta(() => RenderShellEvidence(
            1366,
            768,
            AppThemePreference.Light,
            RouteRegistry.SettingsRouteKey,
            new UnsafePluginPageModel()));

        evidence.SurfaceNames.ShouldContain("PluginPageHost");
        evidence.Text.ShouldNotContain(UnsafePluginPageModel.LeakedText);
    }

    [Fact]
    public void Settings_surface_hides_enabled_user_management_actions_when_unavailable()
    {
        var strings = HostStringCatalog.CreateResourceBacked();
        var settingsEvidence = RunOnSta(() => RenderShellEvidence(
            1366,
            768,
            AppThemePreference.Light,
            RouteRegistry.SettingsRouteKey,
            userManagementAvailable: false));

        settingsEvidence.SurfaceNames.ShouldNotContain("UserManagementSettingsSurface");
        settingsEvidence.SurfaceNames.ShouldNotContain("UserManagementActionList");
        settingsEvidence.Text.ShouldNotContain(strings.GetString(HostResourceKeys.SettingsUserManagement));
        settingsEvidence.Text.ShouldNotContain(strings.GetString(HostResourceKeys.UserManagementPermissionDenied));
        settingsEvidence.Text.ShouldNotContain(strings.GetString(HostResourceKeys.UserManagementCreateManagedUser));
        settingsEvidence.Text.ShouldNotContain(strings.GetString(HostResourceKeys.UserManagementResetPassword));
        settingsEvidence.EnabledButtonText.ShouldNotContain(strings.GetString(HostResourceKeys.UserManagementCreateManagedUser));
        settingsEvidence.EnabledButtonText.ShouldNotContain(strings.GetString(HostResourceKeys.UserManagementResetPassword));
    }

    [Fact]
    public void Plugins_surface_renders_fault_status_summary_and_detail_text()
    {
        var strings = HostStringCatalog.CreateResourceBacked();
        var evidence = RunOnSta(() => RenderShellEvidence(
            1366,
            768,
            AppThemePreference.Light,
            RouteRegistry.PluginsRouteKey,
            pluginManagerItems:
            [
                new ShellPluginManagerItem(
                    "com.dobby.sample",
                    "Sample Plugin",
                    "1.0.0",
                    "Verification plugin",
                    IsEnabled: true,
                    "Faulted",
                    IsPendingUninstall: false,
                    RestartRequired: false)
            ]));

        var faulted = strings.GetString(HostResourceKeys.PluginsManagerStatusFaulted);
        evidence.Text.ShouldContain(faulted);
        evidence.Text.ShouldContain($"Sample Plugin 1.0.0 - {faulted}");
    }

    [Fact]
    public async Task Shell_route_host_maps_plugin_route_activation_exceptions_to_safe_failure()
    {
        var host = new ShellRouteHost(
            routeKey => routeKey ?? RouteRegistry.HomeRouteKey,
            _ => throw new InvalidOperationException("raw plugin fault detail"));

        var result = await host.ActivateOrHomeAsync("plugin/com.dobby.sample/fault");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(Dobby.Core.ErrorCode.PluginInternalError);
    }

    private static RenderedShellEvidence RenderShellEvidence(
        int width,
        int height,
        AppThemePreference theme,
        string activeRouteKey = "plugin.sample.analytics.daily",
        IPluginPageModel? pluginPageModel = null,
        bool userManagementAvailable = true,
        IReadOnlyList<ShellPluginManagerItem>? pluginManagerItems = null)
    {
        var strings = HostStringCatalog.CreateResourceBacked();
        var shell = new ShellViewModel(
            strings,
            SnapshotContributions());
        shell.ActivateRoute(activeRouteKey).IsSuccess.ShouldBeTrue();
        shell.SetTheme(theme);
        shell.SetSettingsAndAbout(
            [
                new ShellSettingsCategoryItem("appearance", strings.GetString(HostResourceKeys.SettingsAppearance), true),
                new ShellSettingsCategoryItem("account", strings.GetString(HostResourceKeys.SettingsAccount), true),
                new ShellSettingsCategoryItem("security-session", strings.GetString(HostResourceKeys.SettingsSecuritySession), true),
                new ShellSettingsCategoryItem("user-management", strings.GetString(HostResourceKeys.SettingsUserManagement), userManagementAvailable),
                new ShellSettingsCategoryItem("about", strings.GetString(HostResourceKeys.SettingsAbout), true)
            ],
            [
                new ShellAboutField("app-version", strings.GetString(HostResourceKeys.AboutAppVersion), "7.0.0-test"),
                new ShellAboutField("build-number", strings.GetString(HostResourceKeys.AboutBuildNumber), "100"),
                new ShellAboutField("build-date", strings.GetString(HostResourceKeys.AboutBuildDate), "2026-07-09"),
                new ShellAboutField("host-edition", strings.GetString(HostResourceKeys.AboutHostEdition), "local"),
                new ShellAboutField("host-channel", strings.GetString(HostResourceKeys.AboutHostChannel), "stable"),
                new ShellAboutField("device-code", strings.GetString(HostResourceKeys.AboutDeviceCode), "device-redacted-1234"),
                new ShellAboutField("plugins", strings.GetString(HostResourceKeys.AboutPlugins), strings.GetString(HostResourceKeys.AboutPluginsEmpty)),
                new ShellAboutField("support-contact", strings.GetString(HostResourceKeys.AboutSupportContact), "support@example.invalid"),
                new ShellAboutField("legal-line", strings.GetString(HostResourceKeys.AboutLegalLine), "Copyright Dobby"),
                new ShellAboutField("owner-identity", strings.GetString(HostResourceKeys.AboutOwnerIdentity), strings.GetString(HostResourceKeys.AboutOwnerStatusManaged))
            ],
            autoLockEnabled: true,
            autoLockMinutes: 12);
        shell.SetLicenseSurface(
            canViewStatus: true,
            canImport: true,
            PluginRuntimeMode.ReadOnlyExpired);
        shell.SetPluginsManagerSurface(
            canView: true,
            canImport: true,
            canConfigure: true,
            canDelete: true,
            pluginManagerItems ??
            [
                new ShellPluginManagerItem(
                    "com.dobby.default",
                    "Default Plugin",
                    "1.0.0",
                    "Default plugin entry",
                    IsEnabled: true,
                    "Enabled",
                    IsPendingUninstall: false,
                    RestartRequired: false)
            ]);
        shell.ShowBackgroundOperation(new BackgroundOperationSnapshot(
            Guid.NewGuid(),
            BackgroundOperationOwnerKind.Host,
            null,
            HostResourceKeys.ShellStatusReady,
            null,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Indeterminate(),
            CanCancel: false,
            Guid.NewGuid(),
            FailureReason: null));
        shell.SetActivePluginPage(pluginPageModel);

        var window = new MainWindow
        {
            DataContext = shell,
            Width = width,
            Height = height,
            Left = -32000,
            Top = -32000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize
        };

        try
        {
            AddThemeResources(window, theme);
            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var profileButton = FindVisuals<Button>(root: window)
                .FirstOrDefault(button => button.Name.Equals("ProfileMenuButton", StringComparison.Ordinal));
            profileButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();

            var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rendered.Render(window);

            var root = (FrameworkElement)window.Content;
            return new RenderedShellEvidence(
                FindVisuals<FrameworkElement>(root)
                    .Where(element => !string.IsNullOrWhiteSpace(element.Name) && element.IsVisible)
                    .Select(element => element.Name)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                FindVisuals<Button>(root)
                    .Where(button => button.IsVisible)
                    .Select(button => button.Tag as string)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .ToArray()!,
                FindVisuals<TextBlock>(root)
                    .Where(text => text.IsVisible)
                    .Select(text => text.Text)
                    .Concat(FindVisuals<Button>(root)
                        .Where(button => button.IsVisible)
                        .Select(button => button.Content?.ToString()))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Select(text => text!)
                    .ToArray(),
                FindVisuals<Button>(root)
                    .Where(button => button.IsVisible && button.IsEnabled)
                    .Select(button => button.Content?.ToString())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Select(text => text!)
                    .ToArray(),
                CountUniqueSampledColors(rendered));
        }
        finally
        {
            window.Close();
        }
    }

    private static IReadOnlyList<string> RequiredSurfaces()
    {
        return
        [
            "TopToolbar",
            "Sidebar",
            "ThirdBar",
            "MainContent",
            "ShellStatusBar",
            "ShellNotificationHost",
            "ShellFeedbackHost",
            "MinimizedWorkSessionStrip",
            "ProfileMenu"
        ];
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

    private static void AddThemeResources(Window window, AppThemePreference theme)
    {
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/Dobby.UI;component/Themes/DesignTokens.xaml", UriKind.Relative)
        });
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"/Dobby.UI;component/Themes/{theme}Theme.xaml", UriKind.Relative)
        });
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

    private static int CountUniqueSampledColors(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        var colors = new HashSet<int>();

        for (var offset = 0; offset < pixels.Length; offset += 4 * 97)
        {
            colors.Add(
                pixels[offset] |
                (pixels[offset + 1] << 8) |
                (pixels[offset + 2] << 16) |
                (pixels[offset + 3] << 24));
        }

        return colors.Count;
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
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

        return result!;
    }

    private sealed record RenderedShellEvidence(
        IReadOnlyList<string> SurfaceNames,
        IReadOnlyList<string> RouteTags,
        IReadOnlyList<string> Text,
        IReadOnlyList<string> EnabledButtonText,
        int UniqueSampledColors);

    private sealed class UnsafePluginPageModel : IPluginPageModel
    {
        public const string LeakedText = "plugin supplied control leaked";

        public override string ToString()
        {
            return LeakedText;
        }
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
}

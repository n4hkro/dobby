using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dobby.Configuration;
using Dobby.Contracts.Ordering;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Host;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell;
using Dobby.Shell.Layout;
using Shouldly;

namespace Dobby.IntegrationTests.Shell;

public sealed class ShellSnapshotTests
{
    [Theory]
    [InlineData(1024, 680, AppThemePreference.Light)]
    [InlineData(1920, 1080, AppThemePreference.Dark)]
    public void Shell_snapshot_descriptor_covers_required_surfaces_and_nested_navigation_without_plugin_activation(
        int width,
        int height,
        AppThemePreference theme)
    {
        var state = ShellLayoutState.CreateDefault(NavigationComposer.Compose(SnapshotContributions()));

        var snapshot = state.CreateSnapshotDescriptor(width, height, theme);

        snapshot.ShouldContain($"viewport={width}x{height}");
        snapshot.ShouldContain($"theme={theme}");
        snapshot.ShouldContain("surface=TopToolbar");
        snapshot.ShouldContain("surface=Sidebar");
        snapshot.ShouldContain("surface=ThirdBar");
        snapshot.ShouldContain("node=sample.analytics.daily.filters,target=ThirdBar");
    }

    [Theory]
    [InlineData(1024, 680, AppThemePreference.Light)]
    [InlineData(1920, 1080, AppThemePreference.Dark)]
    public void Shell_rendered_window_covers_required_surfaces_settings_about_and_nested_navigation_without_plugin_activation(
        int width,
        int height,
        AppThemePreference theme)
    {
        var evidence = RunOnSta(() => RenderShellEvidence(width, height, theme));

        evidence.SurfaceNames.ShouldBe([
            "TopToolbar",
            "Sidebar",
            "ThirdBar",
            "MainContent",
            "BottomStatus",
            "MinimizedWorkStrip"
        ], ignoreOrder: true);
        evidence.RouteTags.ShouldContain(RouteRegistry.HomeRouteKey);
        evidence.RouteTags.ShouldContain(RouteRegistry.PluginsRouteKey);
        evidence.RouteTags.ShouldContain(RouteRegistry.SettingsRouteKey);
        evidence.RouteTags.ShouldContain("plugin.sample.analytics");
        evidence.RouteTags.ShouldContain("plugin.sample.analytics.daily");
        evidence.RouteTags.ShouldContain("plugin.sample.analytics.daily.filters");
        evidence.Text.ShouldContain("Appearance");
        evidence.Text.ShouldContain("Account");
        evidence.Text.ShouldContain("Security / Session");
        evidence.Text.ShouldContain("About");
        evidence.Text.ShouldContain("Host edition/channel");
        evidence.Text.ShouldContain("Plugin versions/status");
        evidence.Text.ShouldContain("Registered owner");
        evidence.Text.ShouldContain("Qısa bildiriş");
        evidence.Text.ShouldContain("Banner bildirişi");
        evidence.Text.ShouldContain("Modal bildiriş");
        evidence.Text.ShouldContain("Yüklənir");
        evidence.Text.ShouldContain("Məlumat yoxdur");
        evidence.Text.ShouldContain("Uğurlu");
        evidence.Text.ShouldContain("Xəta");
        evidence.Text.ShouldContain("Təsdiq");
        evidence.UniqueSampledColors.ShouldBeGreaterThan(3);
    }

    [Theory]
    [InlineData(1366, 768, AppThemePreference.Light, 96, 96)]
    [InlineData(1920, 1080, AppThemePreference.Dark, 120, 120)]
    [InlineData(2560, 1440, AppThemePreference.Light, 96, 96)]
    [InlineData(3840, 2160, AppThemePreference.Dark, 96, 96)]
    [InlineData(1920, 1080, AppThemePreference.Light, 144, 144)]
    public void Shell_rendered_window_covers_resolution_and_dpi_matrix(
        int width,
        int height,
        AppThemePreference theme,
        double dpiX,
        double dpiY)
    {
        var evidence = RunOnSta(() => RenderShellEvidence(width, height, theme, dpiX, dpiY));

        evidence.SurfaceNames.ShouldContain("TopToolbar");
        evidence.SurfaceNames.ShouldContain("Sidebar");
        evidence.SurfaceNames.ShouldContain("ThirdBar");
        evidence.SurfaceNames.ShouldContain("MainContent");
        evidence.SurfaceNames.ShouldContain("BottomStatus");
        evidence.Text.ShouldContain("Qısa bildiriş");
        evidence.Text.ShouldContain("Təsdiq");
        evidence.UniqueSampledColors.ShouldBeGreaterThan(3);
    }

    private static RenderedShellEvidence RenderShellEvidence(
        int width,
        int height,
        AppThemePreference theme,
        double dpiX = 96,
        double dpiY = 96)
    {
        var shell = new ShellViewModel(
            HostStringCatalog.CreateResourceBacked(),
            SnapshotContributions());
        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();
        shell.SetTheme(theme);
        shell.SetSettingsAndAbout(
            [
                new ShellSettingsCategoryItem("appearance", "Appearance", true),
                new ShellSettingsCategoryItem("account", "Account", true),
                new ShellSettingsCategoryItem("security-session", "Security / Session", true),
                new ShellSettingsCategoryItem("user-management", "User Management", true),
                new ShellSettingsCategoryItem("about", "About", true)
            ],
            [
                new ShellAboutField("version", "App version", "7.0.0-test"),
                new ShellAboutField("build", "Build number/date", "sprint7-rendered"),
                new ShellAboutField("edition", "Host edition/channel", "Desktop / local"),
                new ShellAboutField("device", "Device code", "redacted-1234"),
                new ShellAboutField("plugins", "Plugin versions/status", "No plugins installed"),
                new ShellAboutField("support", "Support contact", "support@example.invalid"),
                new ShellAboutField("legal", "Legal", "Local verification"),
                new ShellAboutField("owner", "Registered owner", "Registered")
            ]);
        shell.ShowBackgroundOperation(new BackgroundOperationSnapshot(
            Guid.NewGuid(),
            BackgroundOperationOwnerKind.Host,
            null,
            "Dobby.Shell.Status.Ready",
            null,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Indeterminate(),
            CanCancel: false,
            Guid.NewGuid(),
            FailureReason: null));

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
            window.UpdateLayout();

            var rendered = new RenderTargetBitmap(
                width,
                height,
                dpiX,
                dpiY,
                PixelFormats.Pbgra32);
            rendered.Render(window);

            var root = (DependencyObject)window.Content;
            return new RenderedShellEvidence(
                FindVisuals<FrameworkElement>(root)
                    .Where(element => RequiredSurfaces().Contains(element.Name, StringComparer.Ordinal) &&
                        element.ActualWidth > 0 &&
                        element.ActualHeight > 0)
                    .Select(element => element.Name)
                    .ToArray(),
                FindVisuals<Button>(root)
                    .Select(button => button.Tag as string)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .ToArray()!,
                FindVisuals<TextBlock>(root)
                    .Select(text => text.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToArray(),
                CountUniqueSampledColors(rendered));
        }
        finally
        {
            window.Close();
        }
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

    private static IReadOnlyList<string> RequiredSurfaces()
    {
        return
        [
            "TopToolbar",
            "Sidebar",
            "ThirdBar",
            "MainContent",
            "BottomStatus",
            "MinimizedWorkStrip"
        ];
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
        int UniqueSampledColors);
}

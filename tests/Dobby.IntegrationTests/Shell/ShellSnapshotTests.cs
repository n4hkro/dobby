using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dobby.Configuration;
using Dobby.Contracts.Ordering;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
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
    public void Shell_rendered_window_keeps_only_titlebar_tabs_and_header_navigation_visible(
        int width,
        int height,
        AppThemePreference theme)
    {
        var evidence = RunOnSta(() => RenderShellEvidence(width, height, theme));

        AssertMinimalShellSurface(evidence);
        evidence.RouteTags.ShouldContain(RouteRegistry.HomeRouteKey);
        evidence.RouteTags.ShouldContain(RouteRegistry.PluginsRouteKey);
        evidence.RouteTags.ShouldContain(RouteRegistry.SettingsRouteKey);
        evidence.RouteTags.ShouldNotContain("plugin.sample.analytics.daily");
        evidence.RouteTags.ShouldNotContain("plugin.sample.analytics.daily.filters");
        evidence.Text.ShouldContain("Dobby");
        evidence.Text.ShouldContain("Ana səhifə");
        evidence.Text.ShouldContain("Pluginlər");
        evidence.Text.ShouldContain("Tənzimləmələr");
        evidence.Text.ShouldNotContain("Cases Overview");
        evidence.Text.ShouldNotContain("Plugin lisenziyası");
        evidence.Text.ShouldNotContain("Təsdiq");
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

        AssertMinimalShellSurface(evidence);
        evidence.Text.ShouldNotContain("Qısa bildiriş");
        evidence.Text.ShouldNotContain("Təsdiq");
        evidence.UniqueSampledColors.ShouldBeGreaterThan(3);
    }

    [Theory]
    [InlineData(1024, 680, AppThemePreference.Light)]
    [InlineData(1920, 1080, AppThemePreference.Dark)]
    public void Shell_rendered_home_route_does_not_render_removed_body_regions(
        int width,
        int height,
        AppThemePreference theme)
    {
        var evidence = RunOnSta(() => RenderShellEvidence(width, height, theme, activateNestedRoute: false));

        AssertMinimalShellSurface(evidence);
        evidence.SurfaceNames.ShouldNotContain("HomeDashboardHeader");
        evidence.SurfaceNames.ShouldNotContain("HomePipelineBoard");
        evidence.SurfaceNames.ShouldNotContain("HomeMetricPanels");
        evidence.SurfaceNames.ShouldNotContain("PipelineSubnav");
        evidence.Text.ShouldNotContain("Cases Overview");
        evidence.Text.ShouldNotContain("Pipelines");
        evidence.Text.ShouldNotContain("Davam edir");
        evidence.Text.ShouldNotContain("Tamamlandı");
        evidence.Text.ShouldNotContain("Vaxtı keçib");
    }

    [Fact]
    public void Shell_rendered_nested_plugin_route_keeps_sidebar_empty_and_third_bar_removed()
    {
        var evidence = RunOnSta(() => RenderShellEvidence(
            1366,
            768,
            AppThemePreference.Dark,
            activateNestedRoute: false,
            activeRouteKey: "plugin.sample.analytics.daily"));

        AssertMinimalShellSurface(evidence);
        evidence.RouteTags.ShouldContain(RouteRegistry.HomeRouteKey);
        evidence.RouteTags.ShouldContain(RouteRegistry.PluginsRouteKey);
        evidence.RouteTags.ShouldContain(RouteRegistry.SettingsRouteKey);
        evidence.RouteTags.ShouldNotContain("plugin.sample.analytics.daily");
        evidence.RouteTags.ShouldNotContain("plugin.sample.analytics.daily.filters");
        evidence.Text.ShouldNotContain("Dobby.Plugin.Sample.Analytics.Daily.Filters");
    }

    private static RenderedShellEvidence RenderShellEvidence(
        int width,
        int height,
        AppThemePreference theme,
        double dpiX = 96,
        double dpiY = 96,
        bool activateNestedRoute = true,
        IReadOnlyList<NavigationContribution>? contributions = null,
        string? activeRouteKey = null)
    {
        var shell = new ShellViewModel(
            HostStringCatalog.CreateResourceBacked(),
            contributions ?? SnapshotContributions());
        if (!string.IsNullOrWhiteSpace(activeRouteKey))
        {
            shell.ActivateRoute(activeRouteKey).IsSuccess.ShouldBeTrue();
        }
        else if (activateNestedRoute)
        {
            shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();
        }

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
        shell.SetLicenseSurface(
            canViewStatus: true,
            canImport: true,
            PluginRuntimeMode.ReadOnlyExpired);
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

            var root = (FrameworkElement)window.Content;
            var surfaceBounds = FindVisuals<FrameworkElement>(root)
                .Where(element => RequiredSurfaces().Contains(element.Name, StringComparer.Ordinal) &&
                    element.IsVisible &&
                    element.ActualWidth > 0 &&
                    element.ActualHeight > 0)
                .GroupBy(element => element.Name, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => BoundsToRoot(group.First(), root),
                        StringComparer.Ordinal);
            var pipelineSubnav = FindVisuals<FrameworkElement>(root)
                .FirstOrDefault(element => string.Equals(element.Name, "PipelineSubnav", StringComparison.Ordinal));
            var pipelineButtonBounds = pipelineSubnav is null
                ? []
                : FindVisuals<Button>(pipelineSubnav)
                    .Where(button => button.IsVisible && button.ActualWidth > 0 && button.ActualHeight > 0)
                    .Select(button => BoundsToRoot(button, root))
                    .ToArray();
            var pathDataBounds = FindVisuals<System.Windows.Shapes.Path>(root)
                .Where(path => !string.IsNullOrWhiteSpace(path.Name) &&
                    path.Data is not null &&
                    !path.Data.Bounds.IsEmpty)
                .ToDictionary(
                    path => path.Name,
                    path => path.TransformToAncestor(root).TransformBounds(path.Data.Bounds),
                    StringComparer.Ordinal);

            return new RenderedShellEvidence(
                surfaceBounds.Keys.ToArray(),
                surfaceBounds,
                pathDataBounds,
                pipelineButtonBounds,
                FindVisuals<Button>(root)
                    .Where(button => button.IsVisible)
                    .Select(button => button.Tag as string)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .ToArray()!,
                FindVisuals<TextBlock>(root)
                    .Where(text => text.IsVisible)
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

    private static IReadOnlyList<NavigationContribution> ThirdBarContributions(int thirdBarItemCount)
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

        var leaves = Enumerable.Range(0, thirdBarItemCount)
            .Select(index => NavigationContribution.Create(
                $"sample.analytics.daily.filters.{index}",
                $"plugin.sample.analytics.daily.filters.{index}",
                $"Filter {index + 1}",
                NavigationTarget.ThirdBar,
                new UiOrderingKey("default", 10 + index, "sample", 2 + index),
                child.Id));

        return [parent, child, .. leaves];
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
            "ShellChrome",
            "WindowTitleBar",
            "WorkspaceTabStrip",
            "WorkspaceNewTabButton",
            "WindowCaptionButtons",
            "TopToolbar",
            "ShellHeader",
            "ShellBrandMark",
            "CenteredTopToolbar",
            "ProfileMenuButton",
            "ShellBody",
            "ShellSidebar",
            "Sidebar",
            "SidebarNavigationList",
            "ThirdBar",
            "ThirdBarNavigationList",
            "MainContent",
            "MainContentPanelFrame",
            "MainContentPanelBorder",
            "MainContentCutoutMask",
            "MainContentCutoutBorder",
            "MainContentBody",
            "HomeDashboardHeader",
            "HomePipelineBoard",
            "HomeMetricPanels",
            "PipelineSubnav",
            "BottomStatus",
            "MinimizedWorkStrip"
        ];
    }

    private static void AssertMinimalShellSurface(RenderedShellEvidence evidence)
    {
        foreach (var surface in new[]
        {
            "ShellChrome",
            "WindowTitleBar",
            "WorkspaceTabStrip",
            "WorkspaceNewTabButton",
            "WindowCaptionButtons",
            "TopToolbar",
            "ShellHeader",
            "ShellBrandMark",
            "CenteredTopToolbar",
            "ProfileMenuButton",
            "ShellBody",
            "ShellSidebar",
            "Sidebar"
        })
        {
            evidence.SurfaceNames.ShouldContain(surface);
        }

        foreach (var surface in new[]
        {
            "SidebarNavigationList",
            "ThirdBar",
            "ThirdBarNavigationList",
            "MainContent",
            "MainContentPanelFrame",
            "MainContentPanelBorder",
            "MainContentCutoutMask",
            "MainContentCutoutBorder",
            "MainContentBody",
            "HomeDashboardHeader",
            "HomePipelineBoard",
            "HomeMetricPanels",
            "PipelineSubnav",
            "BottomStatus",
            "MinimizedWorkStrip"
        })
        {
            evidence.SurfaceNames.ShouldNotContain(surface);
        }
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

    private static Rect BoundsToRoot(FrameworkElement element, Visual root)
    {
        var topLeft = element.TransformToAncestor(root).Transform(new Point(0, 0));
        return new Rect(topLeft.X, topLeft.Y, element.ActualWidth, element.ActualHeight);
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
        IReadOnlyDictionary<string, Rect> SurfaceBounds,
        IReadOnlyDictionary<string, Rect> PathDataBounds,
        IReadOnlyList<Rect> PipelineSubnavButtonBounds,
        IReadOnlyList<string> RouteTags,
        IReadOnlyList<string> Text,
        int UniqueSampledColors);
}

using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Xml.Linq;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class ShellUiStaticScanTests
{
    [Fact]
    public void Host_shell_xaml_does_not_hardcode_visible_strings_or_shell_level_tokens()
    {
        var root = RepoRoot();
        var files = new[]
        {
            Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml")
        };
        var violations = new List<string>();
        var visibleAttributePattern = new Regex("\\b(Content|Text|Header|ToolTip)\\s*=\\s*\"(?!(\\{Binding|\\{DynamicResource|\\{StaticResource))([^\"]+)\"", RegexOptions.Compiled);
        var hardcodedTokenPattern = new Regex("(#[0-9A-Fa-f]{6,8}|\\b(FontSize|Margin|Padding)\\s*=\\s*\"\\d)", RegexOptions.Compiled);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match match in visibleAttributePattern.Matches(text))
            {
                var value = match.Groups[3].Value;
                if (value is "Dobby")
                {
                    continue;
                }

                violations.Add($"{file}: hardcoded visible {match.Groups[1].Value}='{value}'.");
            }

            foreach (Match match in hardcodedTokenPattern.Matches(text))
            {
                violations.Add($"{file}: shell-level hardcoded token '{match.Value}'.");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Host_shell_xaml_declares_required_rendered_regions_and_action_surfaces()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));
        var requiredNames = new[]
        {
            "WindowTitleBar",
            "WorkspaceTabStrip",
            "WorkspaceNewTabButton",
            "WindowCaptionButtons",
            "WindowMinimizeButton",
            "WindowMaximizeRestoreButton",
            "WindowCloseButton",
            "ShellSidebar",
            "ShellBrandMark",
            "CenteredTopToolbar",
            "ShellContentHost",
            "TopToolbar",
            "Sidebar",
            "SidebarNavigationList",
            "ThirdBar",
            "ThirdBarNavigationList",
            "MainContent",
            "HomePanel",
            "PluginsSurface",
            "SettingsSurface",
            "BottomStatus",
            "MinimizedWorkStrip",
            "PluginPageHost",
            "NotificationPresentationList",
            "FeedbackStatePresentationList",
            "WorkSessionWarningPanel",
            "WorkSessionStayButton",
            "WorkSessionMinimizeButton",
            "WorkSessionCloseButton",
            "ProfileMenuButton",
            "ProfileMenuPanel",
            "SettingsCategoryList",
            "AboutFieldList",
            "PluginsManagerPanel",
            "PluginsManagerList",
            "PluginsManagerEmptyText",
            "PluginsManagerDetailText",
            "PluginsManagerInstallButton",
            "PluginsManagerEnableButton",
            "PluginsManagerDisableButton",
            "PluginsManagerUninstallButton",
            "PluginsManagerFeedbackText",
            "LicensePanel",
            "LicenseStatusPanel",
            "LicenseStatusText",
            "LicenseListPanel",
            "LicenseListText",
            "LicenseDetailPanel",
            "LicenseDetailText",
            "LicenseImportButton",
            "LicenseFeedbackText",
            "BackgroundStatusText",
            "LogoutButton"
        };

        foreach (var name in requiredNames)
        {
            shell.ShouldContain($"x:Name=\"{name}\"");
        }

        shell.ShouldContain("Logout_Click");
        shell.ShouldNotContain(string.Concat("Clear", "Saved", "Login", "Button"));
        shell.ShouldNotContain(string.Concat("Clear", "Saved", "Login", "_Click"));
        shell.ShouldContain("Navigation_Click");
        shell.ShouldContain("WorkSessionStay_Click");
        shell.ShouldContain("PluginsManagerInstall_Click");
        shell.ShouldContain("PluginsManagerEnable_Click");
        shell.ShouldContain("PluginsManagerDisable_Click");
        shell.ShouldContain("PluginsManagerUninstall_Click");
        shell.ShouldContain("WorkspaceNewTab_Click");
        shell.ShouldContain("WorkspaceTab_Click");
        shell.ShouldContain("WorkspaceTabClose_Click");
        shell.ShouldContain("UseAeroCaptionButtons=\"False\"");
        shell.ShouldContain("WindowMinimize_Click");
        shell.ShouldContain("WindowMaximizeRestore_Click");
        shell.ShouldContain("WindowClose_Click");
        shell.ShouldContain("x:Name=\"WindowCaptionButtons\"");
        shell.ShouldContain("x:Name=\"WindowMinimizeButton\"");
        shell.ShouldContain("x:Name=\"WindowMaximizeRestoreButton\"");
        shell.ShouldContain("x:Name=\"WindowCloseButton\"");
        shell.ShouldContain("ItemsSource=\"{Binding WorkspaceTabs}\"");
        shell.ShouldContain("AutomationProperties.Name=\"{Binding WorkspaceNewTabLabel}\"");
        shell.ShouldContain("x:Name=\"CenteredTopToolbar\"");
        shell.ShouldContain("IsEnabled=\"{Binding PluginsManagerInstallIsEnabled}\"");
        shell.ShouldContain("IsEnabled=\"{Binding PluginsManagerEnableIsEnabled}\"");
        shell.ShouldContain("IsEnabled=\"{Binding PluginsManagerDisableIsEnabled}\"");
        shell.ShouldContain("IsEnabled=\"{Binding PluginsManagerUninstallIsEnabled}\"");
        shell.ShouldContain("IsEnabled=\"{Binding LicenseImportIsEnabled}\"");
        shell.ShouldContain("WorkSessionMinimize_Click");
        shell.ShouldContain("WorkSessionClose_Click");
        shell.ShouldContain("Tag=\"{Binding RouteKey}\"");
        shell.ShouldContain("Visibility=\"{Binding HomePanelVisibility}\"");
        shell.ShouldContain("Visibility=\"{Binding PluginsSurfaceVisibility}\"");
        shell.ShouldContain("Visibility=\"{Binding SettingsSurfaceVisibility}\"");
    }

    [Fact]
    public void Host_shell_polish_declares_approved_regions_and_uses_vector_icons()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));
        var requiredPolishedNames = new[]
        {
            "ShellChrome",
            "ShellHeader",
            "HomeDashboardHeader",
            "HomePipelineFilters",
            "HomePipelineBoard",
            "HomeMetricPanels",
            "PipelineSubnav"
        };

        foreach (var name in requiredPolishedNames)
        {
            shell.ShouldContain($"x:Name=\"{name}\"");
        }

        shell.ShouldContain("<Path");
        shell.ShouldNotContain("WorkspaceNewTabGlyph");
        shell.ShouldNotContain("WorkspaceTabCloseGlyph");
        shell.ShouldNotContain("WindowMinimizeGlyph");
        shell.ShouldNotContain("WindowMaximizeGlyph");
        shell.ShouldNotContain("WindowCloseGlyph");
    }

    [Fact]
    public void Host_shell_uses_visible_windows_style_caption_buttons_and_chrome_sized_tabs()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));
        var designTokens = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.UI", "Themes", "DesignTokens.xaml"));

        shell.ShouldContain("UseAeroCaptionButtons=\"False\"");
        shell.ShouldContain("GlassFrameThickness=\"0\"");
        shell.ShouldContain("CaptionHeight=\"{DynamicResource Dobby.Shell.ChromeHeight}\"");
        shell.ShouldContain("<ColumnDefinition Width=\"*\" MinWidth=\"0\" />");
        shell.ShouldContain("Width=\"{DynamicResource Dobby.Shell.CaptionButtonReserveGridLength}\"");
        shell.ShouldContain("Grid.Column=\"3\"");
        shell.ShouldContain("Style=\"{StaticResource WindowChromeButtonStyle}\"");
        shell.ShouldContain("Style=\"{StaticResource WindowCloseButtonStyle}\"");
        shell.ShouldContain("WorkspaceTabStripMaxWidthConverter");
        shell.ShouldContain("WorkspaceTabWidthConverter");
        shell.ShouldContain("ClipToBounds=\"True\"");
        shell.ShouldContain("MaxWidth=\"{DynamicResource Dobby.Shell.TabWidth}\"");
        shell.ShouldContain("Height=\"{DynamicResource Dobby.Shell.TabHeight}\"");
        designTokens.ShouldContain("<sys:Double x:Key=\"Dobby.Shell.TabWidth\">240</sys:Double>");
        designTokens.ShouldContain("<sys:Double x:Key=\"Dobby.Shell.TabHeight\">42</sys:Double>");
        designTokens.ShouldContain("<GridLength x:Key=\"Dobby.Shell.CaptionButtonReserveGridLength\">144</GridLength>");
        designTokens.ShouldContain("<sys:Double x:Key=\"Dobby.Shell.CaptionButtonWidth\">46</sys:Double>");
        designTokens.ShouldContain("<sys:Double x:Key=\"Dobby.Shell.CaptionButtonHeight\">32</sys:Double>");
    }

    [Fact]
    public void Host_shell_starts_maximized_and_custom_chrome_uses_monitor_work_area()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var presenterCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));

        mainWindowCode.ShouldContain("WmGetMinMaxInfo = 0x0024");
        mainWindowCode.ShouldContain("MonitorDefaultToNearest = 0x00000002");
        mainWindowCode.ShouldContain("MonitorFromWindow(");
        mainWindowCode.ShouldContain("GetMonitorInfo(");
        mainWindowCode.ShouldContain("ApplyMaximizedWorkArea(hwnd, lParam)");
        mainWindowCode.ShouldContain("ptMaxPosition");
        mainWindowCode.ShouldContain("ptMaxSize");
        mainWindowCode.ShouldContain("Marshal.StructureToPtr");
        mainWindowCode.ShouldContain("public void MaximizeForShellStartup()");
        mainWindowCode.ShouldContain("forceShellStartupMaximized");
        mainWindowCode.ShouldContain("private void SetShellMaximized()");
        mainWindowCode.ShouldContain("private void SetShellNormal()");
        mainWindowCode.ShouldContain("if (forceShellStartupMaximized)");
        presenterCode.ShouldContain("window.MaximizeForShellStartup();");

        var attachRuntime = presenterCode.IndexOf("window.AttachRuntime(", StringComparison.Ordinal);
        var maximize = presenterCode.IndexOf("window.MaximizeForShellStartup();", StringComparison.Ordinal);
        var showDialog = presenterCode.IndexOf("window.ShowDialog();", StringComparison.Ordinal);
        attachRuntime.ShouldBeGreaterThanOrEqualTo(0);
        maximize.ShouldBeGreaterThan(attachRuntime);
        showDialog.ShouldBeGreaterThan(maximize);
    }

    [Fact]
    public void Host_shell_pipeline_subnav_sits_directly_on_shell_background_without_island_treatment()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));

        var pipelineSubnav = NamedElement(shell, "PipelineSubnav");
        pipelineSubnav.ShouldNotContain("Background=");
        pipelineSubnav.ShouldNotContain("BorderBrush=");
        pipelineSubnav.ShouldNotContain("Effect=");
        pipelineSubnav.ShouldNotContain("DropShadowEffect");

        var thirdBar = NamedElement(shell, "ThirdBar");
        thirdBar.ShouldNotContain("Background=");
        thirdBar.ShouldNotContain("BorderBrush=");
        thirdBar.ShouldNotContain("Effect=");
        thirdBar.ShouldNotContain("DropShadowEffect");
    }

    [Fact]
    public void Host_shell_sidebar_and_top_toolbar_share_continuous_background()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));

        NamedElement(shell, "ShellSidebar").ShouldNotContain("Background=");
        NamedElement(shell, "TopToolbar").ShouldNotContain("Background=");
        NamedElement(shell, "CenteredTopToolbar").ShouldNotContain("Background=");
    }

    [Fact]
    public void Host_shell_records_first_paint_and_does_not_force_about_owner_identity()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var workSessionCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Shell", "WorkSessions", "WorkSessionCloseCoordinator.cs"));
        var presenterCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));

        mainWindowCode.ShouldContain("StartupMilestoneNames.ShellFirstPaint");
        mainWindowCode.ShouldContain("ContentRendered");
        mainWindowCode.ShouldContain("HwndSource");
        mainWindowCode.ShouldContain("ScClose");
        mainWindowCode.ShouldContain("WorkSessionCloseCoordinator");
        mainWindowCode.ShouldContain("PluginPageRenderRegistry");
        mainWindowCode.ShouldContain("UpdatePluginPageHost");
        mainWindowCode.ShouldContain("RequestLogout");
        mainWindowCode.ShouldContain("HandleLock()");
        workSessionCode.ShouldContain("WorkSessionCloseReason.Logout");
        workSessionCode.ShouldContain("WorkSessionCloseReason.PluginDisable");
        presenterCode.ShouldContain("ShellLocalizationDiagnosticsRecorder");
        presenterCode.ShouldContain("ShellBackgroundOperationCoordinator");
        presenterCode.ShouldContain("BeginPostLoginActivation");
        presenterCode.ShouldContain("WpfUiDispatcher");
        presenterCode.ShouldNotContain("GetInfo(isOwner: true");
        presenterCode.ShouldContain("UserPermissionStateReader");
    }

    [Fact]
    public void Plugins_manager_uninstall_requires_confirmation_and_explicit_data_choice()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var confirmationCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Plugins", "PluginUninstallConfirmation.cs"));

        mainWindowCode.ShouldContain("IPluginUninstallConfirmation");
        mainWindowCode.ShouldContain("pluginUninstallConfirmation.Confirm(");
        mainWindowCode.ShouldContain("PluginUninstallConfirmationRequest");
        mainWindowCode.ShouldContain("confirmation.DataChoice");
        mainWindowCode.ShouldContain("private async void PluginsManagerUninstall_Click");
        mainWindowCode.ShouldContain("WasActivatedInCurrentProcess(selectedPlugin.PluginId)");
        mainWindowCode.ShouldContain("await RunSelectedPluginLifecycleActionAsync");
        mainWindowCode.ShouldContain("Task.Run(() => action(");
        mainWindowCode.ShouldNotContain("var wasActivatedInCurrentProcess = selectedPlugin.IsEnabled");
        mainWindowCode.ShouldNotContain("PluginUninstallDataChoice.KeepData,\r\n                wasActivatedInCurrentProcess: true");
        mainWindowCode.ShouldNotContain("PluginUninstallDataChoice.KeepData,\n                wasActivatedInCurrentProcess: true");
        confirmationCode.ShouldContain("HostResourceKeys.PluginsManagerUninstallConfirmationMessage");
        confirmationCode.ShouldContain("HostResourceKeys.PluginsManagerUninstallConfirmationTitle");
        confirmationCode.ShouldNotContain("Choose Yes to delete plugin data");
        confirmationCode.ShouldNotContain("\"Uninstall plugin\"");
    }

    [Fact]
    public void Plugins_manager_long_running_lifecycle_actions_report_background_operations()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var presenterCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));

        mainWindowCode.ShouldContain("IBackgroundOperationReporter? backgroundOperations");
        presenterCode.ShouldContain("window.AttachRuntime(");
        mainWindowCode.ShouldContain("backgroundOperations = loginRuntime.BackgroundOperations");
        mainWindowCode.ShouldContain("ReportPluginsManagerBackgroundOperation(");
        mainWindowCode.ShouldContain("backgroundOperations?.Progress.Report(new BackgroundOperationSnapshot(");
        mainWindowCode.ShouldContain("HostResourceKeys.PluginsManagerInstallBusy");
        mainWindowCode.ShouldContain("HostResourceKeys.PluginsManagerEnableBusy");
        mainWindowCode.ShouldContain("HostResourceKeys.PluginsManagerDisableBusy");
        mainWindowCode.ShouldContain("HostResourceKeys.PluginsManagerUninstallBusy");
        mainWindowCode.ShouldContain("private async void PluginsManagerEnable_Click");
        mainWindowCode.ShouldContain("private async void PluginsManagerDisable_Click");
        mainWindowCode.ShouldNotContain("private void RunSelectedPluginLifecycleAction(");
        mainWindowCode.ShouldNotContain("private void RunPluginLifecycleAction(");
        mainWindowCode.ShouldContain("BackgroundOperationStatus.Running");
        mainWindowCode.ShouldContain("BackgroundOperationStatus.Completed");
        mainWindowCode.ShouldContain("BackgroundOperationStatus.Failed");
    }

    [Fact]
    public void License_import_long_running_action_reports_background_operations()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var presenterCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));

        presenterCode.ShouldContain("window.AttachRuntime(");
        mainWindowCode.ShouldContain("backgroundOperations = loginRuntime.BackgroundOperations");
        mainWindowCode.ShouldContain("LicenseImport_Click");
        mainWindowCode.ShouldContain("ReportLicenseImportBackgroundOperation(");
        mainWindowCode.ShouldContain("licenseImportCoordinator.ImportFileAsync");
        mainWindowCode.ShouldContain("HostResourceKeys.LicenseImportAction");
        mainWindowCode.ShouldContain("BackgroundOperationStatus.Running");
        mainWindowCode.ShouldContain("BackgroundOperationStatus.Completed");
        mainWindowCode.ShouldContain("BackgroundOperationStatus.Failed");
        mainWindowCode.ShouldContain("backgroundOperations?.Progress.Report(new BackgroundOperationSnapshot(");
    }

    [Fact]
    public void Theme_resources_include_accessibility_motion_and_focus_tokens()
    {
        var root = RepoRoot();
        var tokenCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.UI", "ThemeTokens.cs"));
        var designTokens = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.UI", "Themes", "DesignTokens.xaml"));
        var lightTheme = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.UI", "Themes", "LightTheme.xaml"));
        var darkTheme = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.UI", "Themes", "DarkTheme.xaml"));

        tokenCode.ShouldContain("FocusBrush");
        tokenCode.ShouldContain("ReducedMotionDuration");
        designTokens.ShouldContain("Dobby.Motion.Reduced");
        designTokens.ShouldContain("Dobby.Focus.Thickness");
        lightTheme.ShouldContain("Dobby.Brush.Focus");
        darkTheme.ShouldContain("Dobby.Brush.Focus");
    }

    [Fact]
    public void Theme_coordinator_uses_runtime_accessibility_preferences()
    {
        var root = RepoRoot();
        var coordinatorCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "ShellThemeCoordinator.cs"));

        coordinatorCode.ShouldContain("ApplyAccessibilityPreferences");
        coordinatorCode.ShouldContain("SystemParameters.HighContrast");
        coordinatorCode.ShouldContain("SystemParameters.ClientAreaAnimation");
    }

    [Fact]
    public void Theme_contrast_tokens_meet_minimum_accessibility_ratio()
    {
        var root = RepoRoot();
        foreach (var themeName in new[] { "LightTheme.xaml", "DarkTheme.xaml" })
        {
            var theme = ThemeBrushes(Path.Combine(root.FullName, "src", "Dobby.UI", "Themes", themeName));
            ContrastRatio(theme["Dobby.Brush.Background"], theme["Dobby.Brush.Text"])
                .ShouldBeGreaterThanOrEqualTo(4.5, $"{themeName} background/text contrast must meet the P1 baseline.");
            ContrastRatio(theme["Dobby.Brush.Surface"], theme["Dobby.Brush.Text"])
                .ShouldBeGreaterThanOrEqualTo(4.5, $"{themeName} surface/text contrast must meet the P1 baseline.");
            ContrastRatio(theme["Dobby.Brush.Background"], theme["Dobby.Brush.Focus"])
                .ShouldBeGreaterThanOrEqualTo(3.0, $"{themeName} focus/background contrast must keep visible focus indicators detectable.");
        }
    }

    private static IReadOnlyDictionary<string, Color> ThemeBrushes(string path)
    {
        var document = XDocument.Load(path);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        return document
            .Descendants(presentation + "SolidColorBrush")
            .ToDictionary(
                element => element.Attribute(xaml + "Key")?.Value ?? string.Empty,
                element => (Color)ColorConverter.ConvertFromString(element.Attribute("Color")?.Value)!,
                StringComparer.Ordinal);
    }

    private static double ContrastRatio(Color first, Color second)
    {
        var lighter = Math.Max(RelativeLuminance(first), RelativeLuminance(second));
        var darker = Math.Min(RelativeLuminance(first), RelativeLuminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static string NamedElement(string xaml, string name)
    {
        var nameIndex = xaml.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
        nameIndex.ShouldBeGreaterThanOrEqualTo(0, $"Expected x:Name=\"{name}\" in MainWindow.xaml.");
        var start = xaml.LastIndexOf('<', nameIndex);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var nextName = xaml.IndexOf("x:Name=\"", nameIndex + name.Length + 8, StringComparison.Ordinal);
        return nextName < 0
            ? xaml[start..]
            : xaml[start..nextName];
    }

    private static double RelativeLuminance(Color color)
    {
        static double Component(byte value)
        {
            var channel = value / 255d;
            return channel <= 0.03928
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Component(color.R) +
            0.7152 * Component(color.G) +
            0.0722 * Component(color.B);
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

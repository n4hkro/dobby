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
            "TopToolbar",
            "Sidebar",
            "SidebarNavigationList",
            "ThirdBar",
            "ThirdBarNavigationList",
            "MainContent",
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
            "LogoutButton",
            "ClearSavedLoginButton"
        };

        foreach (var name in requiredNames)
        {
            shell.ShouldContain($"x:Name=\"{name}\"");
        }

        shell.ShouldContain("Logout_Click");
        shell.ShouldContain("ClearSavedLogin_Click");
        shell.ShouldContain("Navigation_Click");
        shell.ShouldContain("WorkSessionStay_Click");
        shell.ShouldContain("PluginsManagerInstall_Click");
        shell.ShouldContain("PluginsManagerEnable_Click");
        shell.ShouldContain("PluginsManagerDisable_Click");
        shell.ShouldContain("PluginsManagerUninstall_Click");
        shell.ShouldContain("IsEnabled=\"{Binding PluginsManagerInstallIsEnabled}\"");
        shell.ShouldContain("IsEnabled=\"{Binding PluginsManagerEnableIsEnabled}\"");
        shell.ShouldContain("IsEnabled=\"{Binding PluginsManagerDisableIsEnabled}\"");
        shell.ShouldContain("IsEnabled=\"{Binding PluginsManagerUninstallIsEnabled}\"");
        shell.ShouldContain("IsEnabled=\"{Binding LicenseImportIsEnabled}\"");
        shell.ShouldContain("WorkSessionMinimize_Click");
        shell.ShouldContain("WorkSessionClose_Click");
        shell.ShouldContain("Tag=\"{Binding RouteKey}\"");
    }

    [Fact]
    public void Host_shell_records_first_paint_and_does_not_force_about_owner_identity()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var workSessionCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Shell", "WorkSessions", "WorkSessionCloseCoordinator.cs"));
        var appCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "App.xaml.cs"));

        mainWindowCode.ShouldContain("StartupMilestoneNames.ShellFirstPaint");
        mainWindowCode.ShouldContain("ContentRendered");
        mainWindowCode.ShouldContain("WorkSessionCloseCoordinator");
        mainWindowCode.ShouldContain("PluginPageRenderRegistry");
        mainWindowCode.ShouldContain("UpdatePluginPageHost");
        mainWindowCode.ShouldContain("RequestLogout");
        mainWindowCode.ShouldContain("HandleLock()");
        workSessionCode.ShouldContain("WorkSessionCloseReason.Logout");
        workSessionCode.ShouldContain("WorkSessionCloseReason.PluginDisable");
        appCode.ShouldContain("ShellLocalizationDiagnosticsRecorder");
        appCode.ShouldContain("ShellBackgroundOperationCoordinator");
        appCode.ShouldContain("BeginPostLoginActivation");
        appCode.ShouldContain("WpfUiDispatcher");
        appCode.ShouldNotContain("GetInfo(isOwner: true");
        appCode.ShouldContain("UserPermissionStateReader");
    }

    [Fact]
    public void Plugins_manager_uninstall_requires_confirmation_and_explicit_data_choice()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));

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

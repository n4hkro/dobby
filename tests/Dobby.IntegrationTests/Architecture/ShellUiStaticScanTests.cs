using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Xml.Linq;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class ShellUiStaticScanTests
{
    [Fact]
    public void Wpf_integration_test_classes_share_the_nonparallel_collection()
    {
        var testsRoot = Path.Combine(RepoRoot().FullName, "tests", "Dobby.IntegrationTests");
        var wpfTestFiles = Directory.EnumerateFiles(testsRoot, "*Tests.cs", SearchOption.AllDirectories)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .Where(file =>
                file.Source.Contains("RunOnSta(", StringComparison.Ordinal) ||
                file.Source.Contains("CreateHiddenWindow(", StringComparison.Ordinal) ||
                file.Source.Contains("new Window(", StringComparison.Ordinal))
            .ToArray();

        wpfTestFiles.ShouldNotBeEmpty();
        foreach (var file in wpfTestFiles)
        {
            file.Source.Contains(
                "[Collection(WpfIntegrationTestCollection.Name)]",
                StringComparison.Ordinal).ShouldBeTrue(
                    $"WPF/STA integration test {file.Path} must not run in parallel with other test collections.");
        }
    }

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
            "ShellChrome",
            "WindowTitleBar",
            "WorkspaceTabCluster",
            "WorkspaceTabStrip",
            "WorkspaceNewTabButton",
            "WindowCaptionButtons",
            "WindowMinimizeButton",
            "WindowMaximizeRestoreButton",
            "WindowCloseButton",
            "TopToolbar",
            "ShellBrandMark",
            "CenteredTopToolbar",
            "ProfileMenuButton",
            "ShellBody",
            "Sidebar",
            "SidebarNavigationList",
            "ThirdBar",
            "ThirdBarNavigationList",
            "MainContent",
            "HomePanel",
            "PluginsSurface",
            "SettingsSurface",
            "PluginPageHost",
            "ShellStatusBar",
            "ShellNotificationHost",
            "ShellFeedbackHost",
            "MinimizedWorkSessionStrip",
            "ProfileMenu",
            "NotificationPresentationList",
            "FeedbackStatePresentationList",
            "WorkSessionWarningPanel",
            "WorkSessionStayButton",
            "WorkSessionMinimizeButton",
            "WorkSessionCloseButton",
            "SettingsCategoryList",
            "AppearanceSettingsSurface",
            "AppearanceThemeSelector",
            "AccountSettingsSurface",
            "SettingsAccountProfileText",
            "SettingsCurrentPasswordBox",
            "SettingsNewPasswordBox",
            "SettingsConfirmNewPasswordBox",
            "SettingsPasswordChangeRememberMeCheckBox",
            "SettingsChangePasswordButton",
            "SecuritySessionSettingsSurface",
            "SecurityAutoLockEnabledCheckBox",
            "SecurityAutoLockMinutesInput",
            "UserManagementSettingsSurface",
            "UserManagementDeniedText",
            "AboutSettingsSurface",
            "AboutFieldList",
            "PluginsManagerList",
            "PluginsManagerDetailRows",
            "PluginsManagerEmptyText",
            "PluginsManagerInstallButton",
            "PluginsManagerEnableButton",
            "PluginsManagerDisableButton",
            "PluginsManagerUninstallButton",
            "LicensePanel",
            "LicenseImportButton",
            "BackgroundStatusText",
            "LogoutButton",
            "ManualLockButton",
            "ChangePasswordButton",
            "CurrentPasswordBox",
            "NewPasswordBox",
            "ConfirmNewPasswordBox",
            "PasswordChangeRememberMeCheckBox",
            "AccountStatusText",
            "DiagnosticsExportButton"
        };
        var removedNames = new[]
        {
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
            "MinimizedWorkStrip",
            "ProfileMenuPanel",
            "PluginsManagerPanel",
            "PluginsManagerDetailText",
            "PluginsManagerFeedbackText",
            "LicenseStatusPanel",
            "LicenseStatusText",
            "LicenseListPanel",
            "LicenseListText",
            "LicenseDetailPanel",
            "LicenseDetailText",
            "LicenseFeedbackText"
        };

        foreach (var name in requiredNames)
        {
            shell.ShouldContain($"x:Name=\"{name}\"");
        }

        AssertAbsentNamedElements(shell, removedNames);
        shell.ShouldNotContain(string.Concat("Clear", "Saved", "Login", "Button"));
        shell.ShouldNotContain(string.Concat("Clear", "Saved", "Login", "_Click"));
        shell.ShouldContain("Navigation_Click");
        shell.ShouldContain("WorkSessionStay_Click");
        shell.ShouldContain("WorkSessionMinimize_Click");
        shell.ShouldContain("WorkSessionClose_Click");
        shell.ShouldContain("ChangePassword_Click");
        shell.ShouldContain("ManualLock_Click");
        shell.ShouldContain("Logout_Click");
        shell.ShouldContain("DiagnosticsExport_Click");
        shell.ShouldContain("LicenseImport_Click");
        shell.ShouldContain("PluginsManagerInstall_Click");
        shell.ShouldContain("PluginsManagerEnable_Click");
        shell.ShouldContain("PluginsManagerDisable_Click");
        shell.ShouldContain("PluginsManagerUninstall_Click");
        shell.ShouldContain("WorkspaceNewTab_Click");
        shell.ShouldContain("WorkspaceTab_Click");
        shell.ShouldContain("WorkspaceTabClose_Click");
        shell.ShouldContain("UseAeroCaptionButtons=\"False\"");
        shell.ShouldNotContain("UseAeroCaptionButtons=\"True\"");
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
        shell.ShouldContain("Tag=\"{Binding RouteKey}\"");
        shell.ShouldContain("Visibility=\"{Binding HomePanelVisibility}\"");
        shell.ShouldContain("Visibility=\"{Binding PluginsSurfaceVisibility}\"");
        shell.ShouldContain("Visibility=\"{Binding SettingsSurfaceVisibility}\"");
    }

    [Fact]
    public void Host_sidebar_open_node_state_uses_distinct_collapsed_and_expanded_chevrons()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));
        var defaultChevron = Regex.Match(
            shell,
            "Path x:Name=\"SidebarNodeChevron\" Data=\"(?<data>[^\"]+)\"",
            RegexOptions.Singleline);
        var openChevron = Regex.Match(
            shell,
            "DataTrigger Binding=\"\\{Binding IsOpen\\}\" Value=\"True\".*?Setter TargetName=\"SidebarNodeChevron\" Property=\"Data\" Value=\"(?<data>[^\"]+)\"",
            RegexOptions.Singleline);

        defaultChevron.Success.ShouldBeTrue();
        openChevron.Success.ShouldBeTrue();
        openChevron.Groups["data"].Value.ShouldNotBe(defaultChevron.Groups["data"].Value);
    }

    [Fact]
    public void Host_shell_settings_surface_uses_rendered_sections_and_gated_user_management_actions()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));

        foreach (var name in new[]
        {
            "AppearanceSettingsSurface",
            "AppearanceThemeSelector",
            "AccountSettingsSurface",
            "SettingsAccountProfileText",
            "SettingsCurrentPasswordBox",
            "SettingsNewPasswordBox",
            "SettingsConfirmNewPasswordBox",
            "SettingsPasswordChangeRememberMeCheckBox",
            "SettingsChangePasswordButton",
            "SecuritySessionSettingsSurface",
            "SecurityAutoLockEnabledCheckBox",
            "SecurityAutoLockMinutesInput",
            "UserManagementSettingsSurface",
            "UserManagementActionList",
            "UserManagementDeniedText",
            "AboutSettingsSurface",
            "AboutFieldList"
        })
        {
            shell.ShouldContain($"x:Name=\"{name}\"");
        }

        shell.ShouldContain("ItemsSource=\"{Binding AppearanceThemeOptions}\"");
        shell.ShouldContain("IsChecked=\"{Binding SecurityAutoLockEnabled");
        shell.ShouldContain("Text=\"{Binding SecurityAutoLockMinutesText");
        shell.ShouldContain("Visibility=\"{Binding UserManagementSurfaceVisibility}\"");
        shell.ShouldContain("ItemsSource=\"{Binding UserManagementActionItems}\"");
        shell.ShouldContain("IsEnabled=\"{Binding UserManagementActionsEnabled}\"");
        shell.ShouldContain("Visibility=\"{Binding UserManagementActionsVisibility}\"");
        shell.ShouldContain("Visibility=\"{Binding UserManagementDeniedVisibility}\"");
        shell.ShouldNotContain("ItemsSource=\"{Binding UserManagementActionLabels}\"");
    }

    [Fact]
    public void Host_shell_work_session_restore_uses_restore_specific_localized_label()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));
        var keys = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Localization", "HostResourceKeys.cs"));
        var catalog = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Localization", "HostStringCatalog.cs"));

        keys.ShouldContain("ShellWorkSessionRestore");
        catalog.ShouldContain("HostResourceKeys.ShellWorkSessionRestore");
        shell.ShouldContain("WorkSessionRestoreLabel");
        shell.ShouldNotContain("Content=\"{Binding DataContext.WorkSessionMinimizeLabel");
    }

    [Fact]
    public void Host_profile_actions_use_localized_feedback_without_raw_error_codes()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var keys = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Localization", "HostResourceKeys.cs"));
        var profileActions = CodeBetween(mainWindowCode, "private void ChangePassword_Click", "private void QueueThemePreferencePersist");

        foreach (var keyName in new[]
        {
            "ShellProfilePasswordChangeBusy",
            "ShellProfilePasswordChangeDenied",
            "ShellProfilePasswordChangeError",
            "ShellProfilePasswordChangeSuccess",
            "ShellProfileManualLockBusy",
            "ShellProfileManualLockDenied",
            "ShellProfileManualLockError",
            "ShellProfileManualLockSuccess",
            "ShellDiagnosticsExportBusy",
            "ShellDiagnosticsExportDenied",
            "ShellDiagnosticsExportError",
            "ShellDiagnosticsExportSuccess",
            "ShellProfileLogoutBusy",
            "ShellProfileLogoutDenied",
            "ShellProfileLogoutError",
            "ShellProfileLogoutSuccess"
        })
        {
            keys.ShouldContain(keyName);
            profileActions.ShouldContain($"HostResourceKeys.{keyName}");
        }

        mainWindowCode.ShouldContain("SetAccountStatusMessage");
        profileActions.ShouldNotContain("result.ErrorCode?.ToString()");
        profileActions.ShouldNotContain("ErrorCode.InvalidInput.ToString()");
        profileActions.ShouldNotContain("ErrorCode.UnsupportedOperation.ToString()");
        profileActions.ShouldNotContain("ErrorCode.IoFailed.ToString()");
        profileActions.ShouldContain("result = Result.Failure(ErrorCode.UnsupportedOperation);");
        profileActions.ShouldNotContain("result = Result.Success();");
        mainWindowCode.ShouldContain("DiagnosticsExportButton.IsEnabled = canExportDiagnostics;");
    }

    [Fact]
    public void Host_shell_polish_declares_approved_regions_and_uses_vector_icons()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));
        var requiredPolishedNames = new[]
        {
            "ShellChrome",
            "TopToolbar",
            "ShellBody",
            "Sidebar",
            "ThirdBar",
            "MainContent",
            "ShellStatusBar",
            "ShellNotificationHost",
            "ShellFeedbackHost",
            "MinimizedWorkSessionStrip",
            "ProfileMenu"
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
    public void Host_shell_design_surface_renders_task5_regions_without_removed_cutout_layout()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));

        NamedElement(shell, "ShellChrome").ShouldNotContain("Visibility=\"Collapsed\"");
        NamedElement(shell, "TopToolbar").ShouldNotContain("Visibility=\"Collapsed\"");
        NamedElement(shell, "ShellBody").ShouldNotContain("Visibility=\"Hidden\"");
        NamedElement(shell, "ShellBody").ShouldNotContain("Visibility=\"Collapsed\"");
        shell.ShouldContain("x:Name=\"SidebarNavigationList\"");
        shell.ShouldContain("x:Name=\"ThirdBarNavigationList\"");
        shell.ShouldContain("x:Name=\"PluginPageHost\"");
        shell.ShouldNotContain("ShellSubnavCutout");
        shell.ShouldNotContain("ShellMainContentPanelBorderGeometryConverter");
        shell.ShouldNotContain("ThirdBarPillStyle");
        shell.ShouldNotContain("FilterPillStyle");
        shell.ShouldNotContain("SidebarItemButtonStyle");
    }

    [Fact]
    public void Host_shell_uses_visible_caption_buttons_and_chrome_sized_tabs()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));
        var designTokens = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.UI", "Themes", "DesignTokens.xaml"));

        shell.ShouldContain("UseAeroCaptionButtons=\"False\"");
        shell.ShouldNotContain("UseAeroCaptionButtons=\"True\"");
        shell.ShouldContain("GlassFrameThickness=\"0\"");
        shell.ShouldContain("CaptionHeight=\"{DynamicResource Dobby.Shell.ChromeHeight}\"");
        shell.ShouldContain("x:Name=\"WorkspaceTabCluster\"");
        shell.ShouldContain("Width=\"{DynamicResource Dobby.Shell.CaptionButtonReserveGridLength}\"");
        shell.ShouldContain("Grid.Column=\"2\"");
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
        shell.ShouldContain("x:Key=\"WorkspaceNewTabButtonStyle\"");
        shell.ShouldContain("x:Key=\"WorkspaceTabCloseButtonStyle\"");
        shell.ShouldContain("HorizontalContentAlignment\" Value=\"Left\"");
        shell.ShouldContain("<Setter Property=\"Width\" Value=\"28\" />");
        shell.ShouldContain("<Setter Property=\"Height\" Value=\"28\" />");
        shell.ShouldContain("<Setter Property=\"Width\" Value=\"34\" />");
        shell.ShouldContain("<Setter Property=\"Height\" Value=\"34\" />");
        shell.ShouldContain("<Trigger Property=\"IsMouseOver\" Value=\"True\">");
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
    public void Host_shell_body_design_does_not_ship_removed_pipeline_cutout_or_third_bar_markup()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));
        var designTokens = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.UI", "Themes", "DesignTokens.xaml"));
        var lightTheme = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.UI", "Themes", "LightTheme.xaml"));
        var darkTheme = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.UI", "Themes", "DarkTheme.xaml"));

        AssertAbsentNamedElements(
            shell,
            "MainContentPanelFrame",
            "MainContentPanelBorder",
            "MainContentCutoutMetrics",
            "MainContentCutoutMask",
            "MainContentCutoutBorder",
            "PipelineSubnav",
            "ShellContentHost");

        shell.ShouldNotContain("ShellSubnavCutout");
        shell.ShouldNotContain("ShellMainContentPanelBorderGeometryConverter");
        shell.ShouldNotContain("ThirdBarPillStyle");
        designTokens.ShouldNotContain("Dobby.Shell.MainContentTopBandHeight");
        designTokens.ShouldNotContain("Dobby.Shell.SubnavCutoutMaskDepth");
        designTokens.ShouldNotContain("Dobby.Shell.SubnavCutoutHorizontalPadding");
        designTokens.ShouldNotContain("Dobby.Shell.SubnavCutoutBorderStrokeThickness");
        designTokens.ShouldNotContain("Dobby.Shell.SubnavCutoutMargin");
        designTokens.ShouldNotContain("Dobby.Shell.ThirdBarPillMinWidth");
        designTokens.ShouldNotContain("Dobby.Shell.ThirdBarPillMinHeight");
        designTokens.ShouldNotContain("Dobby.Shell.ThirdBarPillPadding");
        designTokens.ShouldNotContain("Dobby.Shell.MainContentBodyInset");
        designTokens.ShouldNotContain("Dobby.Shell.SubnavNotchOffset");
        designTokens.ShouldNotContain("Dobby.Shell.SubnavIslandPadding");
        designTokens.ShouldNotContain("Dobby.Shell.MainContentNotchInset");
        lightTheme.ShouldNotContain("Dobby.Brush.SubnavIsland");
        darkTheme.ShouldNotContain("Dobby.Brush.SubnavIsland");
    }

    [Fact]
    public void Host_shell_sidebar_and_top_toolbar_share_continuous_background()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));

        NamedElement(shell, "TopToolbar").ShouldNotContain("Background=");
        NamedElement(shell, "CenteredTopToolbar").ShouldNotContain("Background=");
        NamedElement(shell, "Sidebar").ShouldContain("Background=\"{DynamicResource Dobby.Brush.Surface}\"");
        NamedElement(shell, "ThirdBar").ShouldContain("Background=\"{DynamicResource Dobby.Brush.Surface}\"");
        NamedElement(shell, "MainContent").ShouldContain("Background=\"{DynamicResource Dobby.Brush.Surface}\"");
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
        mainWindowCode.ShouldContain("PluginPageHost");
        mainWindowCode.ShouldContain("RenderPluginPageHost");
        mainWindowCode.ShouldNotContain("UpdatePluginPageHost");
        mainWindowCode.ShouldContain("RequestRemainingWorkSessionClose");
        mainWindowCode.ShouldContain("workSessionCloseCoordinator.RequestLogout()");
        mainWindowCode.ShouldNotContain("HandleLock()");
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
    public void Profile_account_text_does_not_use_hardcoded_anonymous_or_raw_session_identifiers()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var updateProfileAccountText = CodeBetween(mainWindowCode, "private void UpdateProfileAccountText()", "private void BeginApplicationExit()");

        updateProfileAccountText.ShouldNotContain("\"anonymous\"");
        updateProfileAccountText.ShouldNotContain("session.SessionId");
        updateProfileAccountText.ShouldContain("shellViewModel.ProfileAnonymousLabel");
    }

    [Fact]
    public void Plugins_manager_uninstall_requires_confirmation_and_explicit_data_choice()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var operationCoordinatorCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Plugins", "ShellPluginManagerOperationCoordinator.cs"));
        var confirmationCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Plugins", "PluginUninstallConfirmation.cs"));

        mainWindowCode.ShouldContain("PluginsManagerUninstall_Click");
        mainWindowCode.ShouldContain("PluginsManagerDisable_Click");
        mainWindowCode.ShouldContain("PluginsManagerEnable_Click");
        mainWindowCode.ShouldContain("PluginsManagerInstall_Click");
        mainWindowCode.ShouldContain("PluginUninstallConfirmationRequest");
        mainWindowCode.ShouldContain("confirmation.DataChoice");
        mainWindowCode.ShouldContain("WasPluginActivatedInCurrentProcess?.Invoke(plugin.PluginId)");
        mainWindowCode.ShouldContain("RunSelectedPluginLifecycleActionAsync(");
        mainWindowCode.ShouldContain("ContinuePendingPluginLifecycleActionAsync(");
        mainWindowCode.ShouldContain("RunPluginUninstallAsync(selected.PluginId, confirmation.DataChoice)");
        operationCoordinatorCode.ShouldContain("actions.UninstallPluginAsync(targetPluginId, dataChoice");
        mainWindowCode.ShouldNotContain("Task.Run(() => action(");
        mainWindowCode.ShouldNotContain("var wasActivatedInCurrentProcess = selected.IsEnabled");
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
        var operationCoordinatorCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Plugins", "ShellPluginManagerOperationCoordinator.cs"));

        presenterCode.ShouldContain("window.AttachRuntime(");
        mainWindowCode.ShouldNotContain("IBackgroundOperationReporter? backgroundOperations");
        mainWindowCode.ShouldNotContain("backgroundOperations = loginRuntime.BackgroundOperations");
        mainWindowCode.ShouldContain("ShellPluginManagerOperationCoordinator");
        mainWindowCode.ShouldContain("InstallPackageAsync(");
        mainWindowCode.ShouldContain("ShutdownOperationsAsync()");
        operationCoordinatorCode.ShouldContain("backgroundOperations?.Progress.Report(new BackgroundOperationSnapshot(");
        operationCoordinatorCode.ShouldContain("HostResourceKeys.PluginsManagerInstallBusy");
        operationCoordinatorCode.ShouldContain("HostResourceKeys.PluginsManagerEnableBusy");
        operationCoordinatorCode.ShouldContain("HostResourceKeys.PluginsManagerDisableBusy");
        operationCoordinatorCode.ShouldContain("HostResourceKeys.PluginsManagerUninstallBusy");
        mainWindowCode.ShouldContain("private async void PluginsManagerEnable_Click");
        mainWindowCode.ShouldContain("private async void PluginsManagerDisable_Click");
        mainWindowCode.ShouldContain("private Task<Result> RunSelectedPluginLifecycleActionAsync(");
        mainWindowCode.ShouldContain("private Task<Result> RunPluginLifecycleActionAsync(");
        mainWindowCode.ShouldContain("private async Task ContinuePendingPluginLifecycleActionAsync(");
        operationCoordinatorCode.ShouldContain("BackgroundOperationStatus.Running");
        operationCoordinatorCode.ShouldContain("BackgroundOperationStatus.Completed");
        operationCoordinatorCode.ShouldContain("BackgroundOperationStatus.Failed");
    }

    [Fact]
    public void License_import_long_running_action_uses_busy_state_and_coordinator_seam()
    {
        var root = RepoRoot();
        var mainWindowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var presenterCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));
        var actionCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Plugins", "LicenseImportShellAction.cs"));

        presenterCode.ShouldContain("window.AttachRuntime(");
        mainWindowCode.ShouldNotContain("backgroundOperations = loginRuntime.BackgroundOperations");
        mainWindowCode.ShouldContain("private async void LicenseImport_Click");
        mainWindowCode.ShouldContain("licenseImportAction.ImportAsync");
        actionCode.ShouldContain("Interlocked.CompareExchange");
        actionCode.ShouldContain("shellViewModel.SetLicenseImportBusy(true)");
        actionCode.ShouldContain("importCoordinator.ImportFileAsync");
        mainWindowCode.ShouldNotContain("ReportLicenseImportBackgroundOperation(");
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
        tokenCode.ShouldNotContain("ShellMainContentTopBandHeight");
        tokenCode.ShouldNotContain("ShellSubnavCutoutMaskDepth");
        tokenCode.ShouldNotContain("ShellSubnavCutoutHorizontalPadding");
        tokenCode.ShouldNotContain("ShellSubnavCutoutBorderStrokeThickness");
        tokenCode.ShouldNotContain("ShellSubnavCutoutMargin");
        tokenCode.ShouldNotContain("ShellThirdBarPillMinWidth");
        tokenCode.ShouldNotContain("ShellThirdBarPillMinHeight");
        tokenCode.ShouldNotContain("ShellThirdBarPillPadding");
        tokenCode.ShouldNotContain("ShellSubnavCutoutMaskWidth");
        tokenCode.ShouldNotContain("ShellSubnavCutoutMaskGeometry");
        tokenCode.ShouldNotContain("ShellSubnavCutoutBorderGeometry");
        tokenCode.ShouldNotContain("ShellSubnavCutoutTopLineCover");
        tokenCode.ShouldNotContain("ShellMainContentBodyInset");
        tokenCode.ShouldNotContain("SubnavIslandBrush");
        tokenCode.ShouldNotContain("SubnavIslandBorderBrush");
        tokenCode.ShouldNotContain("ShellSubnavNotchOffset");
        tokenCode.ShouldNotContain("ShellSubnavIslandPadding");
        tokenCode.ShouldNotContain("ShellMainContentNotchInset");
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

    [Fact]
    public void Theme_dictionaries_export_identical_brush_token_key_sets()
    {
        var root = RepoRoot();
        var themesRoot = Path.Combine(root.FullName, "src", "Dobby.UI", "Themes");
        var light = ThemeBrushes(Path.Combine(themesRoot, "LightTheme.xaml")).Keys.Order(StringComparer.Ordinal).ToArray();
        var dark = ThemeBrushes(Path.Combine(themesRoot, "DarkTheme.xaml")).Keys.Order(StringComparer.Ordinal).ToArray();

        light.ShouldNotBeEmpty();
        dark.ShouldBe(
            light,
            "LightTheme.xaml and DarkTheme.xaml must export the same brush token key set; " +
            "an asymmetric token leaves an unresolved resource after a theme switch.");
        light.ShouldAllBe(key => key.StartsWith("Dobby.Brush.", StringComparison.Ordinal));
    }

    [Fact]
    public void Shell_measurement_tokens_keep_caption_and_chrome_invariants()
    {
        var root = RepoRoot();
        var designTokensPath = Path.Combine(root.FullName, "src", "Dobby.UI", "Themes", "DesignTokens.xaml");
        var document = XDocument.Load(designTokensPath);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var tokens = document
            .Root!
            .Elements()
            .Where(element => element.Attribute(xaml + "Key") is not null)
            .ToDictionary(
                element => element.Attribute(xaml + "Key")!.Value,
                element => element.Value.Trim(),
                StringComparer.Ordinal);

        var chromeHeight = double.Parse(tokens["Dobby.Shell.ChromeHeight"], CultureInfo.InvariantCulture);
        var captionButtonWidth = double.Parse(tokens["Dobby.Shell.CaptionButtonWidth"], CultureInfo.InvariantCulture);
        var captionReserve = double.Parse(tokens["Dobby.Shell.CaptionButtonReserveWidth"], CultureInfo.InvariantCulture);
        var captionReserveGridLength = double.Parse(tokens["Dobby.Shell.CaptionButtonReserveGridLength"], CultureInfo.InvariantCulture);

        captionReserve.ShouldBeGreaterThanOrEqualTo(
            captionButtonWidth * 3,
            "The caption reserve must cover minimize/maximize/close so the tab row cannot overlap native caption hit targets.");
        captionReserveGridLength.ShouldBe(captionReserve);

        var frameThickness = tokens["Dobby.Shell.NativeCaptionFrameThickness"]
            .Split(',', StringSplitOptions.TrimEntries)
            .Select(value => double.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
        frameThickness.Length.ShouldBe(4);
        frameThickness[1].ShouldBe(
            chromeHeight,
            "The native caption frame inset must reserve exactly the shell chrome height.");
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

    private static string CodeBetween(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start);
        return source[start..end];
    }

    private static void AssertAbsentNamedElements(string xaml, params string[] names)
    {
        foreach (var name in names)
        {
            xaml.ShouldNotContain($"x:Name=\"{name}\"");
        }
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

using System.Windows;
using System.Windows.Threading;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Diagnostics;
using Dobby.Host.Login;
using Dobby.Host.Plugins;
using Dobby.Host.Settings;
using Dobby.Localization;
using Dobby.Licensing;
using Dobby.Shell.PluginPages;
using Dobby.Security.Authentication;
using Dobby.Shell;
using Dobby.Shell.WorkSessions;

namespace Dobby.Host;

public partial class MainWindow : Window
{
    private readonly PostLoginSessionRuntime? sessionRuntime;
    private readonly SessionRecord? session;
    private readonly SelfServicePasswordChangeCoordinator? passwordChangeCoordinator;
    private readonly AccountSettingsModel? accountSettingsModel;
    private readonly ISettingsPreferenceSource? preferenceSource;
    private ShellViewModel shellViewModel;
    private ShellThemeCoordinator? themeCoordinator;
    private ShellDiagnosticsExportCoordinator? diagnosticsExportCoordinator;
    private readonly PluginPageRenderRegistry pluginPageRenderRegistry;
    private readonly WorkSessionCloseCoordinator? workSessionCloseCoordinator;
    private readonly PluginPageRouteCoordinator? pluginPageRouteCoordinator;
    private readonly HostPluginPackageInstallCoordinator? pluginPackageInstallCoordinator;
    private readonly HostPluginLifecycleCoordinator? pluginLifecycleCoordinator;
    private readonly IPluginPackageFilePicker? pluginPackageFilePicker;
    private readonly LicenseImportCoordinator? licenseImportCoordinator;
    private readonly ILicenseFilePicker? licenseFilePicker;
    private string? diagnosticsSourceDirectory;
    private string? diagnosticsOutputZipPath;
    private DispatcherTimer? sessionTimer;

    public bool ReturnToLoginRequested { get; private set; }

    public StartupMilestone? ShellFirstPaintMilestone { get; private set; }

    public MainWindow()
    {
        pluginPageRenderRegistry = PluginPageRenderRegistry.CreateDefault();
        InitializeComponent();
        shellViewModel = ShellViewModel.CreateDefault(Dobby.Localization.HostStringCatalog.CreateResourceBacked());
        DataContext = shellViewModel;
        UpdatePluginPageHost();
        ContentRendered += (_, _) => RecordShellFirstPaint();
    }

    public MainWindow(
        PostLoginSessionRuntime sessionRuntime,
        SessionRecord? session,
        SelfServicePasswordChangeCoordinator passwordChangeCoordinator)
        : this(
            sessionRuntime,
            session,
            passwordChangeCoordinator,
            ShellViewModel.CreateDefault(Dobby.Localization.HostStringCatalog.CreateResourceBacked()),
            null,
            null,
            null,
            null,
            null,
            null)
    {
    }

    public MainWindow(
        PostLoginSessionRuntime sessionRuntime,
        SessionRecord? session,
        SelfServicePasswordChangeCoordinator passwordChangeCoordinator,
        ShellViewModel shellViewModel,
        AccountSettingsModel? accountSettingsModel,
        ISettingsPreferenceSource? preferenceSource,
        ShellThemeCoordinator? themeCoordinator,
        ShellDiagnosticsExportCoordinator? diagnosticsExportCoordinator,
        string? diagnosticsSourceDirectory,
        string? diagnosticsOutputZipPath,
        WorkSessionCloseCoordinator? workSessionCloseCoordinator = null,
        PluginPageRenderRegistry? pluginPageRenderRegistry = null,
        PluginPageRouteCoordinator? pluginPageRouteCoordinator = null,
        HostPluginPackageInstallCoordinator? pluginPackageInstallCoordinator = null,
        HostPluginLifecycleCoordinator? pluginLifecycleCoordinator = null,
        IPluginPackageFilePicker? pluginPackageFilePicker = null,
        LicenseImportCoordinator? licenseImportCoordinator = null,
        ILicenseFilePicker? licenseFilePicker = null)
        : this()
    {
        this.sessionRuntime = sessionRuntime;
        this.session = session;
        this.passwordChangeCoordinator = passwordChangeCoordinator;
        this.accountSettingsModel = accountSettingsModel;
        this.preferenceSource = preferenceSource;
        this.shellViewModel = shellViewModel;
        this.themeCoordinator = themeCoordinator;
        this.diagnosticsExportCoordinator = diagnosticsExportCoordinator;
        this.workSessionCloseCoordinator = workSessionCloseCoordinator;
        this.diagnosticsSourceDirectory = diagnosticsSourceDirectory;
        this.diagnosticsOutputZipPath = diagnosticsOutputZipPath;
        this.pluginPageRouteCoordinator = pluginPageRouteCoordinator;
        this.pluginPackageInstallCoordinator = pluginPackageInstallCoordinator;
        this.pluginLifecycleCoordinator = pluginLifecycleCoordinator;
        this.pluginPackageFilePicker = pluginPackageFilePicker;
        if (pluginPageRenderRegistry is not null)
        {
            this.pluginPageRenderRegistry = pluginPageRenderRegistry;
        }
        DataContext = shellViewModel;
        UpdateWorkSessionWarningVisibility();
        this.licenseImportCoordinator = licenseImportCoordinator;
        this.licenseFilePicker = licenseFilePicker;
        shellViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ShellViewModel.ActivePluginPageModel))
            {
                UpdatePluginPageHost();
            }
        };
        Loaded += (_, _) =>
        {
            ApplyPersistedShellPreference();
            StartSessionCheckpointTimer();
        };
        Closed += (_, _) =>
        {
            PersistShellPreference();
            sessionTimer?.Stop();
        };
        PreviewKeyDown += (_, _) => RecordSessionActivity();
        PreviewMouseDown += (_, _) => RecordSessionActivity();
    }

    internal Result CheckpointSession()
    {
        if (sessionRuntime is null || session is null)
        {
            return Result.Success();
        }

        var result = sessionRuntime.Checkpoint(session, CorrelationId.New());
        if (!result.IsSuccess)
        {
            ReturnToLoginRequested = true;
            Close();
        }

        return result;
    }

    private void StartSessionCheckpointTimer()
    {
        if (sessionRuntime is null || session is null || sessionTimer is not null)
        {
            return;
        }

        sessionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        sessionTimer.Tick += (_, _) => CheckpointSession();
        sessionTimer.Start();
        CheckpointSession();
    }

    private void RecordSessionActivity()
    {
        if (sessionRuntime is null || session is null)
        {
            return;
        }

        sessionRuntime.RecordActivity();
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (passwordChangeCoordinator is null || session is null)
        {
            ShowAccountStatus(ErrorCode.UnsupportedOperation.ToString());
            return;
        }

        var result = passwordChangeCoordinator.ChangePassword(
            session.UserId,
            session.SessionId,
            NewPasswordBox.Password,
            PasswordChangeRememberMeCheckBox.IsChecked == true,
            CorrelationId.New());
        if (!result.IsSuccess)
        {
            ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.UnsupportedOperation.ToString());
            return;
        }

        NewPasswordBox.Clear();
        ShowAccountStatus(shellViewModel.PasswordChangedLabel);
    }

    private void ManualLock_Click(object sender, RoutedEventArgs e)
    {
        workSessionCloseCoordinator?.HandleLock();
        if (sessionRuntime is null || session is null)
        {
            return;
        }

        var result = sessionRuntime.LockNow(session, CorrelationId.New());
        if (!result.IsSuccess)
        {
            ReturnToLoginRequested = true;
            Close();
        }
    }

    private void ShowAccountStatus(string text)
    {
        AccountStatusText.Text = text;
        AccountStatusText.Visibility = Visibility.Visible;
    }

    private void UpdateWorkSessionWarningVisibility()
    {
        WorkSessionWarningPanel.Visibility = shellViewModel.HasPendingWorkSessionCloseRequest
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdatePluginPageHost()
    {
        if (shellViewModel.ActivePluginPageModel is null)
        {
            PluginPageHost.Content = null;
            PluginPageHost.Visibility = Visibility.Collapsed;
            return;
        }

        var render = pluginPageRenderRegistry.Render(shellViewModel.ActivePluginPageModel);
        if (!render.IsSuccess)
        {
            PluginPageHost.Content = null;
            PluginPageHost.Visibility = Visibility.Collapsed;
            return;
        }

        PluginPageHost.Content = render.Value;
        PluginPageHost.Visibility = Visibility.Visible;
    }

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        shellViewModel.ToggleSidebarCollapsed();
        PersistShellPreference();
    }

    private async void Navigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string routeKey })
        {
            return;
        }

        var pendingClose = workSessionCloseCoordinator?.RequestNavigation(routeKey);
        if (pendingClose is not null)
        {
            shellViewModel.SetPendingWorkSessionCloseRequest(pendingClose);
            UpdateWorkSessionWarningVisibility();
            return;
        }

        var result = pluginPageRouteCoordinator is null
            ? shellViewModel.ActivateRoute(routeKey)
            : await pluginPageRouteCoordinator.ActivateRouteAsync(routeKey, CancellationToken.None);
        if (!result.IsSuccess)
        {
            ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.UnsupportedOperation.ToString());
            return;
        }

        PersistShellPreference();
    }

    private void WorkSessionStay_Click(object sender, RoutedEventArgs e)
    {
        ResolvePendingWorkSessionChoice(WorkSessionCloseChoice.Cancel);
    }

    private void WorkSessionMinimize_Click(object sender, RoutedEventArgs e)
    {
        ResolvePendingWorkSessionChoice(WorkSessionCloseChoice.Minimize);
    }

    private void WorkSessionClose_Click(object sender, RoutedEventArgs e)
    {
        ResolvePendingWorkSessionChoice(WorkSessionCloseChoice.Close);
    }

    private void ResolvePendingWorkSessionChoice(WorkSessionCloseChoice choice)
    {
        if (workSessionCloseCoordinator is null)
        {
            shellViewModel.SetPendingWorkSessionCloseRequest(null);
            UpdateWorkSessionWarningVisibility();
            return;
        }

        var hadPendingRequest = shellViewModel.HasPendingWorkSessionCloseRequest;
        var pendingAction = shellViewModel.PendingWorkSessionCloseRequest?.Action;
        var result = shellViewModel.ResolvePendingWorkSessionClose(choice, workSessionCloseCoordinator);
        if (!result.IsSuccess)
        {
            ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.UnsupportedOperation.ToString());
            return;
        }

        UpdateWorkSessionWarningVisibility();
        if (hadPendingRequest && choice != WorkSessionCloseChoice.Cancel)
        {
            PersistShellPreference();
            if (pendingAction == PendingWorkSessionAction.Logout)
            {
                CompleteLogout();
            }
        }
    }

    private void PluginsManagerSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string pluginId })
        {
            shellViewModel.SelectPlugin(pluginId);
        }
    }

    private async void PluginsManagerInstall_Click(object sender, RoutedEventArgs e)
    {
        if (pluginPackageInstallCoordinator is null || pluginPackageFilePicker is null || session is null)
        {
            shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
            return;
        }

        if (!shellViewModel.PluginsManagerInstallIsEnabled)
        {
            return;
        }

        var selectedPath = pluginPackageFilePicker.PickPluginPackageFile();
        if (selectedPath is null)
        {
            return;
        }

        shellViewModel.SetPluginsManagerBusy(true);
        try
        {
            var result = await Task.Run(() => pluginPackageInstallCoordinator.InstallPackage(
                session.UserId,
                selectedPath,
                new HostVersion(1, 0, 0),
                CorrelationId.New()));
            if (!result.IsSuccess)
            {
                shellViewModel.SetPluginsManagerFeedback(
                    result.ErrorCode == ErrorCode.Forbidden
                        ? HostResourceKeys.PluginsManagerOperationDenied
                        : HostResourceKeys.ShellFeedbackError);
                return;
            }

            RefreshPluginsManagerItems(result.Value.PluginId);
        }
        finally
        {
            shellViewModel.SetPluginsManagerBusy(false);
        }
    }

    private void PluginsManagerEnable_Click(object sender, RoutedEventArgs e)
    {
        RunSelectedPluginLifecycleAction((coordinator, actor, pluginId, correlationId) =>
            coordinator.EnablePlugin(actor, pluginId, correlationId));
    }

    private void PluginsManagerDisable_Click(object sender, RoutedEventArgs e)
    {
        RunSelectedPluginLifecycleAction((coordinator, actor, pluginId, correlationId) =>
            coordinator.DisablePlugin(actor, pluginId, correlationId));
    }

    private void PluginsManagerUninstall_Click(object sender, RoutedEventArgs e)
    {
        RunSelectedPluginLifecycleAction((coordinator, actor, pluginId, correlationId) =>
            coordinator.UninstallPlugin(
                actor,
                pluginId,
                PluginUninstallDataChoice.KeepData,
                wasActivatedInCurrentProcess: true,
                correlationId));
    }

    private void RunSelectedPluginLifecycleAction(
        Func<HostPluginLifecycleCoordinator, string, string, CorrelationId, Result> action)
    {
        if (pluginLifecycleCoordinator is null || session is null || shellViewModel.PluginsManagerSelectedItem is null)
        {
            shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
            return;
        }

        var selectedPluginId = shellViewModel.PluginsManagerSelectedItem.PluginId;
        var result = action(pluginLifecycleCoordinator, session.UserId, selectedPluginId, CorrelationId.New());
        if (!result.IsSuccess)
        {
            shellViewModel.SetPluginsManagerFeedback(
                result.ErrorCode == ErrorCode.Forbidden
                    ? HostResourceKeys.PluginsManagerOperationDenied
                    : HostResourceKeys.ShellFeedbackError);
            return;
        }

        RefreshPluginsManagerItems(selectedPluginId);
    }

    private void RefreshPluginsManagerItems(string? selectedPluginId = null)
    {
        if (pluginLifecycleCoordinator is null || session is null)
        {
            return;
        }

        var list = pluginLifecycleCoordinator.ListPlugins(session.UserId, CorrelationId.New());
        if (!list.IsSuccess)
        {
            shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.ShellFeedbackError);
            return;
        }

        shellViewModel.SetPluginsManagerSurface(
            shellViewModel.PluginsManagerCanView,
            shellViewModel.PluginsManagerCanImport,
            shellViewModel.PluginsManagerCanConfigure,
            shellViewModel.PluginsManagerCanDelete,
            list.Value.Select(ToShellPluginManagerItem).ToArray());
        if (!string.IsNullOrWhiteSpace(selectedPluginId))
        {
            shellViewModel.SelectPlugin(selectedPluginId);
        }
    }

    private static ShellPluginManagerItem ToShellPluginManagerItem(Dobby.PluginFramework.Packaging.InstalledPluginMetadata plugin)
    {
        return new ShellPluginManagerItem(
            plugin.PluginId,
            plugin.DisplayName,
            plugin.Version,
            plugin.Description,
            plugin.IsEnabled,
            plugin.StatusSummary,
            plugin.IsPendingUninstall,
            plugin.RestartRequired);
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        var nextTheme = Equals(shellViewModel.Layout.Theme, AppThemePreference.Dark)
            ? AppThemePreference.Light
            : AppThemePreference.Dark;
        var result = themeCoordinator?.SetTheme(nextTheme) ?? Result.Success();
        if (!result.IsSuccess)
        {
            ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.UnsupportedOperation.ToString());
            return;
        }

        shellViewModel.SetTheme(nextTheme);
        ShellThemeCoordinator.ApplyTheme(System.Windows.Application.Current, nextTheme);
    }

    private void ProfileMenu_Click(object sender, RoutedEventArgs e)
    {
        ProfileMenuPanel.Visibility = ProfileMenuPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var pendingClose = workSessionCloseCoordinator?.RequestLogout();
        if (pendingClose is not null)
        {
            shellViewModel.SetPendingWorkSessionCloseRequest(pendingClose);
            UpdateWorkSessionWarningVisibility();
            return;
        }

        CompleteLogout();
    }

    private void CompleteLogout()
    {
        if (sessionRuntime is null || session is null)
        {
            workSessionCloseCoordinator?.HandleLogout();
            ReturnToLoginRequested = true;
            Close();
            return;
        }

        var result = sessionRuntime.Logout(session, CorrelationId.New());
        if (!result.IsSuccess)
        {
            ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.UnsupportedOperation.ToString());
            return;
        }

        workSessionCloseCoordinator?.HandleLogout();
        ReturnToLoginRequested = true;
        Close();
    }

    private void ClearSavedLogin_Click(object sender, RoutedEventArgs e)
    {
        var result = accountSettingsModel?.ForgetSavedLogin() ?? Result.Failure(ErrorCode.UnsupportedOperation);
        ShowAccountStatus(result.IsSuccess
            ? shellViewModel.ClearSavedLoginLabel
            : result.ErrorCode?.ToString() ?? ErrorCode.UnsupportedOperation.ToString());
    }

    private void DiagnosticsExport_Click(object sender, RoutedEventArgs e)
    {
        if (diagnosticsExportCoordinator is null ||
            session is null ||
            string.IsNullOrWhiteSpace(diagnosticsSourceDirectory) ||
            string.IsNullOrWhiteSpace(diagnosticsOutputZipPath))
        {
            ShowAccountStatus(ErrorCode.UnsupportedOperation.ToString());
            return;
        }

        var result = diagnosticsExportCoordinator.Export(
            session.UserId,
            diagnosticsSourceDirectory,
            diagnosticsOutputZipPath,
            CorrelationId.New());
        ShowAccountStatus(result.IsSuccess
            ? shellViewModel.DiagnosticsExportedLabel
            : result.ErrorCode?.ToString() ?? ErrorCode.IoFailed.ToString());
    }

    private async void LicenseImport_Click(object sender, RoutedEventArgs e)
    {
        if (licenseImportCoordinator is null || licenseFilePicker is null || session is null)
        {
            shellViewModel.SetLicenseImportFeedback(HostResourceKeys.LicenseImportRejected);
            return;
        }

        if (!shellViewModel.LicenseImportIsEnabled)
        {
            return;
        }

        var selectedPath = licenseFilePicker.PickLicenseFile();
        if (selectedPath is null)
        {
            return;
        }

        shellViewModel.SetLicenseImportBusy(true);
        try
        {
            var result = await licenseImportCoordinator.ImportFileAsync(
                session.UserId,
                selectedPath,
                CorrelationId.New());
            if (result.IsSuccess)
            {
                shellViewModel.SetLicenseRuntimeMode(
                    LicenseRuntimeModeMapper.Map(result.LifecycleState),
                    HostResourceKeys.LicenseImportSuccess);
                return;
            }

            shellViewModel.SetLicenseImportFeedback(
                result.ReasonCode == LicenseRejectReasons.PermissionDenied
                    ? HostResourceKeys.LicenseImportPermissionDenied
                    : HostResourceKeys.LicenseImportRejected);
        }
        finally
        {
            shellViewModel.SetLicenseImportBusy(false);
        }
    }

    private void ApplyPersistedShellPreference()
    {
        var load = preferenceSource?.Load();
        if (load is not { IsSuccess: true })
        {
            return;
        }

        var runtimePreference = ShellPreferenceMapper.ToRuntimePreference(load.Value.ShellView);
        shellViewModel.ApplyPreference(runtimePreference);
        Width = runtimePreference.WindowPlacement.Width;
        Height = runtimePreference.WindowPlacement.Height;
        if (runtimePreference.WindowPlacement.Left.HasValue)
        {
            Left = runtimePreference.WindowPlacement.Left.Value;
        }

        if (runtimePreference.WindowPlacement.Top.HasValue)
        {
            Top = runtimePreference.WindowPlacement.Top.Value;
        }
    }

    private void PersistShellPreference()
    {
        var load = preferenceSource?.Load();
        if (load is not { IsSuccess: true })
        {
            return;
        }

        var preferences = load.Value;
        var runtimePreference = shellViewModel.ExportPreference(Width, Height, Left, Top);
        _ = preferenceSource!.Save(preferences with
        {
            ShellView = ShellPreferenceMapper.ToDurablePreference(
                runtimePreference,
                preferences.ShellView.RestoreLastActiveModuleOnStartup)
        });
    }

    private void RecordShellFirstPaint()
    {
        ShellFirstPaintMilestone ??= new StartupMilestone(
            StartupMilestoneNames.ShellFirstPaint,
            DateTimeOffset.UtcNow,
            CorrelationId.New().ToString());
    }
}

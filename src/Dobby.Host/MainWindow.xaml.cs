using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Dobby.Configuration;
using Dobby.Contracts.BackgroundOperations;
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
using Dobby.Shell.Layout;
using Dobby.Shell.WorkSessions;

namespace Dobby.Host;

public partial class MainWindow : Window
{
    private const long SessionActivityWriteIntervalMilliseconds = 15_000;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmSysCommand = 0x0112;
    private const int MonitorDefaultToNearest = 0x00000002;
    private const int ScClose = 0xF060;

    private PostLoginSessionRuntime? sessionRuntime;
    private SessionRecord? session;
    private SelfServicePasswordChangeCoordinator? passwordChangeCoordinator;
    private AccountSettingsModel? accountSettingsModel;
    private ISettingsPreferenceSource? preferenceSource;
    private ShellViewModel shellViewModel = null!;
    private ShellThemeCoordinator? themeCoordinator;
    private ShellDiagnosticsExportCoordinator? diagnosticsExportCoordinator;
    private readonly PluginPageRenderRegistry pluginPageRenderRegistry;
    private WorkSessionCloseCoordinator? workSessionCloseCoordinator;
    private ShellLayoutPreference? initialPreference;
    private PluginPageRouteCoordinator? pluginPageRouteCoordinator;
    private HostPluginPackageInstallCoordinator? pluginPackageInstallCoordinator;
    private HostPluginLifecycleCoordinator? pluginLifecycleCoordinator;
    private IPluginPackageFilePicker? pluginPackageFilePicker;
    private readonly IPluginUninstallConfirmation pluginUninstallConfirmation;
    private readonly IShellWindowCloseConfirmation shellWindowCloseConfirmation;
    private LicenseImportCoordinator? licenseImportCoordinator;
    private ILicenseFilePicker? licenseFilePicker;
    private IBackgroundOperationReporter? backgroundOperations;
    private readonly SemaphoreSlim preferencePersistGate = new(1, 1);
    private int themePreferencePersistVersion;
    private int sessionActivityRecordInFlight;
    private long lastSessionActivityRecordQueuedAtMilliseconds = long.MinValue;
    private PendingPluginLifecycleAction? pendingPluginLifecycleAction;
    private string? diagnosticsSourceDirectory;
    private string? diagnosticsOutputZipPath;
    private DispatcherTimer? sessionTimer;
    private bool forceShellStartupMaximized;
    private bool initialPreferenceExplicitlyProvided;

    public bool ReturnToLoginRequested { get; private set; }

    public StartupMilestone? ShellFirstPaintMilestone { get; private set; }

    public MainWindow()
        : this(CreateDefaultShellViewModel())
    {
    }

    private MainWindow(ShellViewModel initialShellViewModel)
    {
        ArgumentNullException.ThrowIfNull(initialShellViewModel);

        pluginPageRenderRegistry = PluginPageRenderRegistry.CreateDefault();
        pluginUninstallConfirmation = new WpfPluginUninstallConfirmation(this);
        shellWindowCloseConfirmation = new WpfShellWindowCloseConfirmation(this);
        InitializeComponent();
        SetShellViewModel(initialShellViewModel);
        ContentRendered += (_, _) => RecordShellFirstPaint();
        Loaded += (_, _) =>
        {
            ApplyInitialShellPreference();
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

    public MainWindow(
        ShellViewModel shellViewModel,
        WorkSessionCloseCoordinator? workSessionCloseCoordinator = null)
        : this(shellViewModel)
    {
        this.workSessionCloseCoordinator = workSessionCloseCoordinator;
        UpdateWorkSessionWarningVisibility();
    }

    public MainWindow(
        PostLoginSessionRuntime sessionRuntime,
        SessionRecord? session,
        SelfServicePasswordChangeCoordinator passwordChangeCoordinator)
        : this(
            sessionRuntime,
            session,
            passwordChangeCoordinator,
            CreateDefaultShellViewModel(),
            null,
            null,
            null,
            null,
            null,
            null)
    {
    }

    private static ShellViewModel CreateDefaultShellViewModel()
    {
        return ShellViewModel.CreateDefault(Dobby.Localization.HostStringCatalog.CreateResourceBacked());
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
        IPluginUninstallConfirmation? pluginUninstallConfirmation = null,
        IShellWindowCloseConfirmation? shellWindowCloseConfirmation = null,
        LicenseImportCoordinator? licenseImportCoordinator = null,
        ILicenseFilePicker? licenseFilePicker = null,
        IBackgroundOperationReporter? backgroundOperations = null,
        ShellLayoutPreference? initialPreference = null)
        : this(shellViewModel)
    {
        this.sessionRuntime = sessionRuntime;
        this.session = session;
        this.passwordChangeCoordinator = passwordChangeCoordinator;
        this.accountSettingsModel = accountSettingsModel;
        this.preferenceSource = preferenceSource;
        this.themeCoordinator = themeCoordinator;
        this.diagnosticsExportCoordinator = diagnosticsExportCoordinator;
        this.workSessionCloseCoordinator = workSessionCloseCoordinator;
        this.initialPreference = initialPreference;
        this.diagnosticsSourceDirectory = diagnosticsSourceDirectory;
        this.diagnosticsOutputZipPath = diagnosticsOutputZipPath;
        this.pluginPageRouteCoordinator = pluginPageRouteCoordinator;
        this.pluginPackageInstallCoordinator = pluginPackageInstallCoordinator;
        this.pluginLifecycleCoordinator = pluginLifecycleCoordinator;
        this.pluginPackageFilePicker = pluginPackageFilePicker;
        this.pluginUninstallConfirmation = pluginUninstallConfirmation ?? new WpfPluginUninstallConfirmation(this);
        this.shellWindowCloseConfirmation = shellWindowCloseConfirmation ?? new WpfShellWindowCloseConfirmation(this);
        if (pluginPageRenderRegistry is not null)
        {
            this.pluginPageRenderRegistry = pluginPageRenderRegistry;
        }
        DataContext = shellViewModel;
        UpdateWorkSessionWarningVisibility();
        this.licenseImportCoordinator = licenseImportCoordinator;
        this.licenseFilePicker = licenseFilePicker;
        this.backgroundOperations = backgroundOperations;
        initialPreferenceExplicitlyProvided = initialPreference is not null;
    }

    public void AttachRuntime(
        HostLoginRuntime loginRuntime,
        SessionRecord? authenticatedSession,
        ISettingsPreferenceSource scopedPreferenceSource,
        ShellThemeCoordinator runtimeThemeCoordinator,
        ShellLayoutPreference? runtimeInitialPreference,
        WorkSessionCloseCoordinator runtimeWorkSessionCloseCoordinator)
    {
        ArgumentNullException.ThrowIfNull(loginRuntime);
        ArgumentNullException.ThrowIfNull(scopedPreferenceSource);
        ArgumentNullException.ThrowIfNull(runtimeThemeCoordinator);
        ArgumentNullException.ThrowIfNull(runtimeWorkSessionCloseCoordinator);

        sessionRuntime = loginRuntime.PostLoginSessionRuntime;
        session = authenticatedSession;
        passwordChangeCoordinator = loginRuntime.SelfServicePasswordChangeCoordinator;
        accountSettingsModel = loginRuntime.AccountSettingsModel;
        preferenceSource = scopedPreferenceSource;
        themeCoordinator = runtimeThemeCoordinator;
        diagnosticsExportCoordinator = loginRuntime.DiagnosticsExportCoordinator;
        diagnosticsSourceDirectory = loginRuntime.DiagnosticsSourceDirectory;
        diagnosticsOutputZipPath = loginRuntime.DiagnosticsOutputZipPath;
        workSessionCloseCoordinator = runtimeWorkSessionCloseCoordinator;
        pluginPageRouteCoordinator = new PluginPageRouteCoordinator(
            loginRuntime.PluginRuntimeCoordinator.ExtensionPointRegistry,
            shellViewModel,
            () => session?.UserId);
        pluginPackageInstallCoordinator = loginRuntime.PluginPackageInstallCoordinator;
        pluginLifecycleCoordinator = loginRuntime.PluginLifecycleCoordinator;
        pluginPackageFilePicker = new WindowsPluginPackageFilePicker();
        licenseImportCoordinator = loginRuntime.LicenseImportCoordinator;
        licenseFilePicker = new WindowsLicenseFilePicker();
        backgroundOperations = loginRuntime.BackgroundOperations;
        initialPreference = runtimeInitialPreference;
        initialPreferenceExplicitlyProvided = true;

        UpdateWorkSessionWarningVisibility();
    }

    public void ApplyRuntimeShellPreference(ShellLayoutPreference runtimePreference)
    {
        ApplyShellPreference(runtimePreference);
    }

    public void ShowRuntimeInitializationFailure()
    {
        ShowAccountStatus(ErrorCode.DatabaseUnavailable.ToString());
    }

    public void MaximizeForShellStartup()
    {
        forceShellStartupMaximized = true;
        SetShellMaximized();
    }

    private void SetShellViewModel(ShellViewModel nextViewModel)
    {
        ArgumentNullException.ThrowIfNull(nextViewModel);

        if (shellViewModel is not null)
        {
            shellViewModel.PropertyChanged -= ShellViewModel_PropertyChanged;
        }

        shellViewModel = nextViewModel;
        shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;
        DataContext = shellViewModel;
        UpdatePluginPageHost();
        UpdateWorkSessionWarningVisibility();
    }

    private void ShellViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellViewModel.ActivePluginPageModel))
        {
            UpdatePluginPageHost();
        }
    }

    private void BeginApplicationExit()
    {
        if (!ReturnToLoginRequested)
        {
            ProcessExitEscalation.Begin();
        }
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmGetMinMaxInfo)
        {
            handled = ApplyMaximizedWorkArea(hwnd, lParam);
            return IntPtr.Zero;
        }

        if (message == WmSysCommand &&
            (wParam.ToInt32() & 0xFFF0) == ScClose &&
            !ReturnToLoginRequested)
        {
            if (!shellWindowCloseConfirmation.ConfirmClose())
            {
                handled = true;
                return IntPtr.Zero;
            }

            BeginApplicationExit();
        }

        return IntPtr.Zero;
    }

    private static bool ApplyMaximizedWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MONITORINFO
        {
            cbSize = Marshal.SizeOf<MONITORINFO>()
        };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var workArea = monitorInfo.rcWork;
        var monitorArea = monitorInfo.rcMonitor;

        minMaxInfo.ptMaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
        minMaxInfo.ptMaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
        minMaxInfo.ptMaxSize.X = Math.Abs(workArea.Right - workArea.Left);
        minMaxInfo.ptMaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);

        Marshal.StructureToPtr(minMaxInfo, lParam, fDeleteOld: true);
        return true;
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
    }

    private void RecordSessionActivity()
    {
        var runtime = sessionRuntime;
        if (runtime is null || session is null)
        {
            return;
        }

        var now = Environment.TickCount64;
        var previous = Volatile.Read(ref lastSessionActivityRecordQueuedAtMilliseconds);
        if (previous != long.MinValue &&
            now - previous < SessionActivityWriteIntervalMilliseconds)
        {
            return;
        }

        QueueSessionActivityRecord(runtime, now);
    }

    private void QueueSessionActivityRecord(PostLoginSessionRuntime runtime, long queuedAtMilliseconds)
    {
        if (Interlocked.CompareExchange(ref sessionActivityRecordInFlight, 1, 0) != 0)
        {
            return;
        }

        Volatile.Write(ref lastSessionActivityRecordQueuedAtMilliseconds, queuedAtMilliseconds);
        _ = Task.Run(() => RecordSessionActivityCore(runtime))
            .ContinueWith(
                _ => Interlocked.Exchange(ref sessionActivityRecordInFlight, 0),
                TaskScheduler.Default);
    }

    private static Result RecordSessionActivityCore(PostLoginSessionRuntime runtime)
    {
        try
        {
            return runtime.RecordActivity();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
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
        QueueShellPreferencePersist();
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

        QueueShellPreferencePersist();
    }

    private void WorkspaceNewTab_Click(object sender, RoutedEventArgs e)
    {
        shellViewModel.OpenWorkspaceTab();
        UpdatePluginPageHost();
        QueueShellPreferencePersist();
    }

    private void WorkspaceTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid tabId })
        {
            return;
        }

        var result = shellViewModel.ActivateWorkspaceTab(tabId);
        if (!result.IsSuccess)
        {
            ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.NotFound.ToString());
            return;
        }

        UpdatePluginPageHost();
        QueueShellPreferencePersist();
    }

    private void WorkspaceTabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid tabId })
        {
            return;
        }

        var result = shellViewModel.CloseWorkspaceTab(tabId);
        if (!result.IsSuccess)
        {
            ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.NotFound.ToString());
            return;
        }

        UpdatePluginPageHost();
        QueueShellPreferencePersist();
    }

    private void WindowMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void WindowMaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowMaximizeRestore();
    }

    private void WindowTitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowMaximizeRestore();
            return;
        }

        DragMove();
    }

    private void ToggleWindowMaximizeRestore()
    {
        if (WindowState == WindowState.Maximized)
        {
            SetShellNormal();
            return;
        }

        SetShellMaximized();
    }

    private void SetShellMaximized()
    {
        WindowState = WindowState.Maximized;
    }

    private void SetShellNormal()
    {
        WindowState = WindowState.Normal;
    }

    private void WindowClose_Click(object sender, RoutedEventArgs e)
    {
        if (shellWindowCloseConfirmation.ConfirmClose())
        {
            BeginApplicationExit();
            Close();
        }
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
        var queuedPluginLifecycleAction = pendingAction is PendingWorkSessionAction.PluginDisable or PendingWorkSessionAction.PluginUninstall
            ? pendingPluginLifecycleAction
            : null;
        var result = shellViewModel.ResolvePendingWorkSessionClose(choice, workSessionCloseCoordinator);
        if (!result.IsSuccess)
        {
            ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.UnsupportedOperation.ToString());
            return;
        }

        if (choice == WorkSessionCloseChoice.Cancel || queuedPluginLifecycleAction is not null)
        {
            pendingPluginLifecycleAction = null;
        }

        UpdateWorkSessionWarningVisibility();
        if (hadPendingRequest && choice != WorkSessionCloseChoice.Cancel)
        {
            QueueShellPreferencePersist();
            if (pendingAction == PendingWorkSessionAction.Logout)
            {
                CompleteLogout();
            }
            else if (queuedPluginLifecycleAction is not null)
            {
                _ = RunPendingPluginLifecycleActionAsync(queuedPluginLifecycleAction);
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
        var operationId = Guid.CreateVersion7();
        var correlationId = CorrelationId.New();
        ReportPluginsManagerBackgroundOperation(
            operationId,
            correlationId,
            ownerId: null,
            HostResourceKeys.PluginsManagerInstallBusy,
            BackgroundOperationStatus.Running,
            failureReason: null);
        try
        {
            var result = await Task.Run(() => pluginPackageInstallCoordinator.InstallPackage(
                session.UserId,
                selectedPath,
                new HostVersion(1, 0, 0),
                correlationId));
            if (!result.IsSuccess)
            {
                ReportPluginsManagerBackgroundOperation(
                    operationId,
                    correlationId,
                    ownerId: null,
                    HostResourceKeys.PluginsManagerInstallBusy,
                    BackgroundOperationStatus.Failed,
                    result.ErrorCode);
                shellViewModel.SetPluginsManagerFeedback(
                    result.ErrorCode == ErrorCode.Forbidden
                        ? HostResourceKeys.PluginsManagerOperationDenied
                        : HostResourceKeys.ShellFeedbackError);
                return;
            }

            ReportPluginsManagerBackgroundOperation(
                operationId,
                correlationId,
                result.Value.PluginId,
                HostResourceKeys.PluginsManagerInstallBusy,
                BackgroundOperationStatus.Completed,
                failureReason: null);
            RefreshPluginsManagerItems(result.Value.PluginId);
        }
        finally
        {
            shellViewModel.SetPluginsManagerBusy(false);
        }
    }

    private async void PluginsManagerEnable_Click(object sender, RoutedEventArgs e)
    {
        await RunSelectedPluginLifecycleActionAsync(
            (coordinator, actor, pluginId, correlationId) =>
                coordinator.EnablePlugin(actor, pluginId, correlationId),
            HostResourceKeys.PluginsManagerEnableBusy);
    }

    private async void PluginsManagerDisable_Click(object sender, RoutedEventArgs e)
    {
        if (TryQueueSelectedPluginLifecycleWorkSessionClose(
            PendingPluginLifecycleOperation.Disable,
            PluginUninstallDataChoice.KeepData))
        {
            return;
        }

        await RunSelectedPluginLifecycleActionAsync(
            (coordinator, actor, pluginId, correlationId) =>
                coordinator.DisablePlugin(actor, pluginId, correlationId),
            HostResourceKeys.PluginsManagerDisableBusy);
    }

    private async void PluginsManagerUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (pluginLifecycleCoordinator is null ||
            session is null ||
            shellViewModel.PluginsManagerSelectedItem is not { } selectedPlugin)
        {
            shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
            return;
        }

        var wasActivatedInCurrentProcess = pluginLifecycleCoordinator.WasActivatedInCurrentProcess(selectedPlugin.PluginId);
        var confirmation = pluginUninstallConfirmation.Confirm(new PluginUninstallConfirmationRequest(
            selectedPlugin.PluginId,
            selectedPlugin.DisplayName,
            wasActivatedInCurrentProcess));
        if (!confirmation.Confirmed)
        {
            return;
        }

        if (TryQueueSelectedPluginLifecycleWorkSessionClose(
            PendingPluginLifecycleOperation.Uninstall,
            confirmation.DataChoice))
        {
            return;
        }

        await RunSelectedPluginLifecycleActionAsync((coordinator, actor, pluginId, correlationId) =>
            coordinator.UninstallPlugin(
                actor,
                pluginId,
                confirmation.DataChoice,
                correlationId),
            HostResourceKeys.PluginsManagerUninstallBusy);
    }

    private async Task RunSelectedPluginLifecycleActionAsync(
        Func<HostPluginLifecycleCoordinator, string, string, CorrelationId, Result> action,
        string displayNameKey)
    {
        if (pluginLifecycleCoordinator is null || session is null || shellViewModel.PluginsManagerSelectedItem is null)
        {
            shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
            return;
        }

        var selectedPluginId = shellViewModel.PluginsManagerSelectedItem.PluginId;
        await RunPluginLifecycleActionAsync(selectedPluginId, action, displayNameKey);
    }

    private async Task RunPluginLifecycleActionAsync(
        string pluginId,
        Func<HostPluginLifecycleCoordinator, string, string, CorrelationId, Result> action,
        string displayNameKey)
    {
        if (pluginLifecycleCoordinator is null || session is null)
        {
            shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
            return;
        }

        var coordinator = pluginLifecycleCoordinator;
        var actor = session.UserId;
        shellViewModel.SetPluginsManagerBusy(true, displayNameKey);
        var operationId = Guid.CreateVersion7();
        var correlationId = CorrelationId.New();
        ReportPluginsManagerBackgroundOperation(
            operationId,
            correlationId,
            pluginId,
            displayNameKey,
            BackgroundOperationStatus.Running,
            failureReason: null);
        try
        {
            var result = await Task.Run(() => action(coordinator, actor, pluginId, correlationId));
            if (!result.IsSuccess)
            {
                ReportPluginsManagerBackgroundOperation(
                    operationId,
                    correlationId,
                    pluginId,
                    displayNameKey,
                    BackgroundOperationStatus.Failed,
                    result.ErrorCode);
                shellViewModel.SetPluginsManagerFeedback(
                    result.ErrorCode == ErrorCode.Forbidden
                        ? HostResourceKeys.PluginsManagerOperationDenied
                        : HostResourceKeys.ShellFeedbackError);
                return;
            }

            ReportPluginsManagerBackgroundOperation(
                operationId,
                correlationId,
                pluginId,
                displayNameKey,
                BackgroundOperationStatus.Completed,
                failureReason: null);
            RefreshPluginsManagerItems(pluginId);
        }
        finally
        {
            shellViewModel.SetPluginsManagerBusy(false, displayNameKey);
        }
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

    private void ReportPluginsManagerBackgroundOperation(
        Guid operationId,
        CorrelationId correlationId,
        string? ownerId,
        string displayNameKey,
        BackgroundOperationStatus status,
        ErrorCode? failureReason)
    {
        var progress = status == BackgroundOperationStatus.Completed
            ? BackgroundOperationProgress.Determinate(100)
            : BackgroundOperationProgress.Indeterminate();
        backgroundOperations?.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Host,
            ownerId,
            displayNameKey,
            ShortDetailKey: null,
            status,
            progress,
            CanCancel: false,
            correlationId.Value,
            failureReason));
    }

    private bool TryQueueSelectedPluginLifecycleWorkSessionClose(
        PendingPluginLifecycleOperation operation,
        PluginUninstallDataChoice dataChoice)
    {
        if (pluginLifecycleCoordinator is null ||
            session is null ||
            workSessionCloseCoordinator is null ||
            shellViewModel.PluginsManagerSelectedItem is not { } selectedPlugin)
        {
            return false;
        }

        var pendingClose = operation == PendingPluginLifecycleOperation.Uninstall
            ? workSessionCloseCoordinator.RequestPluginUninstall(selectedPlugin.PluginId)
            : workSessionCloseCoordinator.RequestPluginDisable(selectedPlugin.PluginId);
        if (pendingClose is null)
        {
            return false;
        }

        pendingPluginLifecycleAction = new PendingPluginLifecycleAction(
            selectedPlugin.PluginId,
            operation,
            dataChoice);
        shellViewModel.SetPendingWorkSessionCloseRequest(pendingClose);
        UpdateWorkSessionWarningVisibility();
        return true;
    }

    private Task RunPendingPluginLifecycleActionAsync(PendingPluginLifecycleAction action)
    {
        if (action.Operation == PendingPluginLifecycleOperation.Uninstall)
        {
            return RunPluginLifecycleActionAsync(
                action.PluginId,
                (coordinator, actor, pluginId, correlationId) => coordinator.UninstallPlugin(
                    actor,
                    pluginId,
                    action.DataChoice,
                    correlationId),
                HostResourceKeys.PluginsManagerUninstallBusy);
        }

        return RunPluginLifecycleActionAsync(
            action.PluginId,
            (coordinator, actor, pluginId, correlationId) =>
                coordinator.DisablePlugin(actor, pluginId, correlationId),
            HostResourceKeys.PluginsManagerDisableBusy);
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        var nextTheme = Equals(shellViewModel.Layout.Theme, AppThemePreference.Dark)
            ? AppThemePreference.Light
            : AppThemePreference.Dark;
        shellViewModel.SetTheme(nextTheme);
        ShellThemeCoordinator.ApplyTheme(System.Windows.Application.Current, nextTheme);
        StartupThemeSnapshotStore.SaveTheme(nextTheme);
        QueueThemePreferencePersist(nextTheme);
    }

    private void QueueThemePreferencePersist(AppThemePreference nextTheme)
    {
        var coordinator = themeCoordinator;
        if (coordinator is null)
        {
            return;
        }

        var persistVersion = Interlocked.Increment(ref themePreferencePersistVersion);
        _ = Task.Run(async () =>
        {
            Result result;
            try
            {
                await preferencePersistGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    result = coordinator.SetTheme(nextTheme);
                }
                finally
                {
                    preferencePersistGate.Release();
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                result = Result.Failure(ErrorCode.DatabaseUnavailable);
            }

            if (result.IsSuccess || persistVersion != Volatile.Read(ref themePreferencePersistVersion))
            {
                return;
            }

            _ = Dispatcher.InvokeAsync(() =>
                ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.UnsupportedOperation.ToString()));
        });
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
        var operationId = Guid.CreateVersion7();
        var correlationId = CorrelationId.New();
        ReportLicenseImportBackgroundOperation(
            operationId,
            correlationId,
            BackgroundOperationStatus.Running,
            failureReason: null);
        try
        {
            var result = await licenseImportCoordinator.ImportFileAsync(
                session.UserId,
                selectedPath,
                correlationId);
            if (result.IsSuccess)
            {
                ReportLicenseImportBackgroundOperation(
                    operationId,
                    correlationId,
                    BackgroundOperationStatus.Completed,
                    failureReason: null);
                shellViewModel.SetLicenseRuntimeMode(
                    LicenseRuntimeModeMapper.Map(result.LifecycleState),
                    HostResourceKeys.LicenseImportSuccess);
                return;
            }

            ReportLicenseImportBackgroundOperation(
                operationId,
                correlationId,
                BackgroundOperationStatus.Failed,
                ToLicenseImportErrorCode(result));
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

    private void ReportLicenseImportBackgroundOperation(
        Guid operationId,
        CorrelationId correlationId,
        BackgroundOperationStatus status,
        ErrorCode? failureReason)
    {
        var progress = status == BackgroundOperationStatus.Completed
            ? BackgroundOperationProgress.Determinate(100)
            : BackgroundOperationProgress.Indeterminate();
        backgroundOperations?.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Host,
            OwnerId: null,
            HostResourceKeys.LicenseImportAction,
            ShortDetailKey: null,
            status,
            progress,
            CanCancel: false,
            correlationId.Value,
            failureReason));
    }

    private static ErrorCode ToLicenseImportErrorCode(LicenseImportResult result)
    {
        return result.ReasonCode == LicenseRejectReasons.PermissionDenied
            ? ErrorCode.Forbidden
            : ErrorCode.ValidationFailed;
    }

    private void ApplyInitialShellPreference()
    {
        if (initialPreference is not null)
        {
            ApplyShellPreference(initialPreference);
            return;
        }

        if (initialPreferenceExplicitlyProvided)
        {
            return;
        }

        ApplyPersistedShellPreference();
    }

    private void ApplyPersistedShellPreference()
    {
        var load = preferenceSource?.Load();
        if (load is not { IsSuccess: true })
        {
            return;
        }

        ApplyShellPreference(ShellPreferenceMapper.ToRuntimePreference(load.Value.ShellView));
    }

    private void ApplyShellPreference(ShellLayoutPreference runtimePreference)
    {
        shellViewModel.ApplyPreference(runtimePreference);

        if (forceShellStartupMaximized)
        {
            SetShellMaximized();
            return;
        }

        ApplyWindowPlacement(runtimePreference.WindowPlacement);
    }

    private void ApplyWindowPlacement(ShellWindowPlacement windowPlacement)
    {
        Width = windowPlacement.Width;
        Height = windowPlacement.Height;
        if (windowPlacement.Left.HasValue)
        {
            Left = windowPlacement.Left.Value;
        }

        if (windowPlacement.Top.HasValue)
        {
            Top = windowPlacement.Top.Value;
        }
    }

    private void QueueShellPreferencePersist()
    {
        if (preferenceSource is null)
        {
            return;
        }

        var source = preferenceSource;
        var runtimePreference = shellViewModel.ExportPreference(Width, Height, Left, Top);
        _ = Task.Run(async () =>
        {
            try
            {
                await preferencePersistGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    PersistShellPreference(source, runtimePreference);
                }
                finally
                {
                    preferencePersistGate.Release();
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                // Interactive clicks must not wait on preference persistence; close still performs a final synchronous save.
            }
        });
    }

    private void PersistShellPreference()
    {
        if (preferenceSource is null)
        {
            return;
        }

        var runtimePreference = shellViewModel.ExportPreference(Width, Height, Left, Top);
        preferencePersistGate.Wait();
        try
        {
            PersistShellPreference(preferenceSource, runtimePreference);
        }
        finally
        {
            preferencePersistGate.Release();
        }
    }

    private static void PersistShellPreference(
        ISettingsPreferenceSource preferenceSource,
        ShellLayoutPreference runtimePreference)
    {
        var load = preferenceSource.Load();
        if (load is not { IsSuccess: true })
        {
            return;
        }

        var preferences = load.Value;
        _ = preferenceSource.Save(preferences with
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

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    private enum PendingPluginLifecycleOperation
    {
        Disable,
        Uninstall
    }

    private sealed record PendingPluginLifecycleAction(
        string PluginId,
        PendingPluginLifecycleOperation Operation,
        PluginUninstallDataChoice DataChoice);
}

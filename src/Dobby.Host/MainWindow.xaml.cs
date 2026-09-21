using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Dobby.Configuration;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Diagnostics;
using Dobby.Host.Login;
using Dobby.Host.Plugins;
using Dobby.Host.Settings;
using Dobby.Localization;
using Dobby.Licensing;
using Dobby.Navigation;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authentication;
using Dobby.Shell;
using Dobby.Shell.Layout;
using Dobby.Shell.PluginPages;
using Dobby.Shell.WorkSessions;

namespace Dobby.Host;

public partial class MainWindow : Window
{
    private const long SessionActivityWriteIntervalMilliseconds = 15_000;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmSysCommand = 0x0112;
    private const int MonitorDefaultToNearest = 0x00000002;
    private const int ScClose = 0xF060;

    private readonly IShellWindowCloseConfirmation shellWindowCloseConfirmation;
    private readonly SemaphoreSlim preferencePersistGate = new(1, 1);
    private readonly CancellationTokenSource windowLifetime = new();
    private readonly object uiOperationGate = new();
    private readonly HashSet<Task> activeUiOperations = [];
    private PluginPageRenderRegistry pluginPageRenderRegistry = PluginPageRenderRegistry.CreateDefault();
    private readonly ShellActionOverrides actionOverrides;

    private HostLoginRuntime? loginRuntime;
    private PostLoginSessionRuntime? sessionRuntime;
    private SessionRecord? session;
    private ISettingsPreferenceSource? preferenceSource;
    private ShellViewModel shellViewModel = null!;
    private ShellThemeCoordinator? themeCoordinator;
    private ShellLayoutPreference? initialPreference;
    private ShellRouteHost? shellRouteHost;
    private WorkSessionRegistry? workSessionRegistry;
    private WorkSessionCloseCoordinator? workSessionCloseCoordinator;
    private LicenseImportShellAction? licenseImportAction;
    private ShellPluginManagerOperationCoordinator? pluginManagerOperationCoordinator;
    private Guid? pendingConfirmedUninstallRequestId;
    private PluginUninstallDataChoice? pendingConfirmedUninstallDataChoice;
    private DispatcherTimer? sessionTimer;
    private bool forceShellStartupMaximized;
    private bool initialPreferenceExplicitlyProvided;
    private bool profileMenuOpen;
    private int themePreferencePersistVersion;
    private int sessionActivityRecordInFlight;
    private long lastSessionActivityRecordQueuedAtMilliseconds = long.MinValue;

    public bool ReturnToLoginRequested { get; private set; }

    public StartupMilestone? ShellFirstPaintMilestone { get; private set; }

    public MainWindow()
        : this(CreateDefaultShellViewModel())
    {
    }

    public MainWindow(ShellViewModel initialShellViewModel, ShellActionOverrides? actionOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(initialShellViewModel);

        this.actionOverrides = actionOverrides ?? new ShellActionOverrides();
        shellWindowCloseConfirmation = new WpfShellWindowCloseConfirmation(this);
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;
        SetShellViewModel(initialShellViewModel);
        ContentRendered += (_, _) => RecordShellFirstPaint();
        Loaded += (_, _) =>
        {
            ApplyInitialShellPreference();
            StartSessionCheckpointTimer();
            RefreshWorkSessions();
            UpdateProfileAccountText();
            UpdateShellVisualState();
        };
        Closed += (_, _) =>
        {
            windowLifetime.Cancel();
            pluginManagerOperationCoordinator?.CancelActiveOperation();
            loginRuntime?.LicenseImportCoordinator.CancelLicenseImport();
            PersistShellPreference();
            sessionTimer?.Stop();
            if (shellViewModel is not null)
            {
                shellViewModel.PropertyChanged -= ShellViewModel_PropertyChanged;
            }
        };
        PreviewKeyDown += (_, _) => RecordSessionActivity();
        PreviewMouseDown += (_, _) => RecordSessionActivity();
    }

    public MainWindow(
        ShellViewModel shellViewModel,
        ISettingsPreferenceSource? preferenceSource,
        ShellThemeCoordinator? themeCoordinator,
        IShellWindowCloseConfirmation? shellWindowCloseConfirmation = null,
        ShellLayoutPreference? initialPreference = null,
        PostLoginSessionRuntime? sessionRuntime = null,
        SessionRecord? session = null,
        ShellActionOverrides? actionOverrides = null,
        WorkSessionCloseCoordinator? workSessionCloseCoordinator = null)
        : this(shellViewModel, actionOverrides)
    {
        this.sessionRuntime = sessionRuntime;
        this.session = session;
        this.preferenceSource = preferenceSource;
        this.themeCoordinator = themeCoordinator;
        this.initialPreference = initialPreference;
        this.workSessionCloseCoordinator = workSessionCloseCoordinator;
        this.shellWindowCloseConfirmation = shellWindowCloseConfirmation ?? new WpfShellWindowCloseConfirmation(this);
        initialPreferenceExplicitlyProvided = initialPreference is not null;
    }

    private static ShellViewModel CreateDefaultShellViewModel()
    {
        return ShellViewModel.CreateDefault(Dobby.Localization.HostStringCatalog.CreateResourceBacked());
    }

    public void AttachRuntime(
        HostLoginRuntime loginRuntime,
        SessionRecord? authenticatedSession,
        ISettingsPreferenceSource scopedPreferenceSource,
        ShellThemeCoordinator runtimeThemeCoordinator,
        ShellLayoutPreference? runtimeInitialPreference,
        ShellRouteHost? runtimeRouteHost = null,
        WorkSessionRegistry? runtimeWorkSessionRegistry = null,
        WorkSessionCloseCoordinator? runtimeWorkSessionCloseCoordinator = null)
    {
        ArgumentNullException.ThrowIfNull(loginRuntime);
        ArgumentNullException.ThrowIfNull(scopedPreferenceSource);
        ArgumentNullException.ThrowIfNull(runtimeThemeCoordinator);

        this.loginRuntime = loginRuntime;
        var authenticatedLifetime = loginRuntime.AuthenticatedLifetime?.Token ??
            throw new InvalidOperationException("Authenticated host lifetime is unavailable.");
        var commandDispatcher = new HostPluginCommandDispatcher(
            loginRuntime.PluginRuntimeCoordinator.ExtensionPointRegistry,
            loginRuntime.OperationAuthorizationPolicy,
            loginRuntime.RuntimeModeMutationGate,
            loginRuntime.BackgroundOperations,
            authenticatedLifetime,
            loginRuntime.PluginRuntimeCoordinator.DiagnosticsCoordinator,
            loginRuntime.PluginRuntimeCoordinator.RuntimeFaultPolicy,
            loginRuntime.PluginRuntimeCoordinator.PostLoginPluginActivator,
            loginRuntime.PluginRuntimeCoordinator.AuditCoordinator);
        pluginPageRenderRegistry = PluginPageRenderRegistry.CreateDefault(
            commandDispatcher);
        Action<string, string, string, ErrorCode>? actionFaultSink =
            loginRuntime.PluginRuntimeCoordinator.DiagnosticsCoordinator is { } diagnostics
                ? diagnostics.RecordPageActionFault
                : null;
        sessionRuntime = loginRuntime.PostLoginSessionRuntime;
        session = authenticatedSession;
        preferenceSource = scopedPreferenceSource;
        themeCoordinator = runtimeThemeCoordinator;
        initialPreference = runtimeInitialPreference;
        initialPreferenceExplicitlyProvided = true;
        workSessionRegistry = runtimeWorkSessionRegistry;
        workSessionCloseCoordinator = runtimeWorkSessionCloseCoordinator;
        Action<ErrorCode>? licenseImportFaultSink = actionFaultSink is null
            ? null
            : errorCode => actionFaultSink(
                "host",
                RouteRegistry.PluginsRouteKey,
                "license.import",
                errorCode);
        licenseImportAction = new LicenseImportShellAction(
            shellViewModel,
            new WindowsLicenseFilePicker(),
            loginRuntime.LicenseImportCoordinator,
            _ => loginRuntime.DispatchPendingPostCommitEffectsAsync(),
            licenseImportFaultSink);
        loginRuntime.StartLicenseActivationRefreshDispatch(RefreshLicenseModeAfterImport);
        pluginManagerOperationCoordinator = CreatePluginManagerOperationCoordinator(authenticatedLifetime);
        ConfigureShellRouteHost(runtimeRouteHost);
        RefreshWorkSessions();
        UpdateProfileAccountText();
        UpdateShellVisualState();
    }

    public void ApplyRuntimeShellPreference(ShellLayoutPreference runtimePreference)
    {
        ApplyShellPreference(runtimePreference);
    }

    public void ShowRuntimeInitializationFailure()
    {
        ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
    }

    public void MaximizeForShellStartup()
    {
        forceShellStartupMaximized = true;
        SetShellMaximized();
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ShellViewModel nextViewModel && !ReferenceEquals(nextViewModel, shellViewModel))
        {
            SetShellViewModel(nextViewModel);
        }
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
        if (!ReferenceEquals(DataContext, nextViewModel))
        {
            DataContext = nextViewModel;
        }

        ConfigureShellRouteHost();
        UpdateShellVisualState();
    }

    private void ConfigureShellRouteHost(ShellRouteHost? runtimeRouteHost = null)
    {
        shellRouteHost = runtimeRouteHost ?? new ShellRouteHost(
            shellViewModel.ResolveRouteOrHome,
            routeKey => ValueTask.FromResult(shellViewModel.ActivateResolvedRoute(routeKey)));
    }

    private void ShellViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.Layout):
            case nameof(ShellViewModel.IsSidebarCollapsed):
            case nameof(ShellViewModel.ThirdBarNavigation):
            case nameof(ShellViewModel.ActivePluginPageModel):
            case nameof(ShellViewModel.PendingWorkSessionCloseRequest):
            case nameof(ShellViewModel.MinimizedWorkSessions):
                UpdateShellVisualState();
                break;
        }
    }

    private void UpdateShellVisualState()
    {
        if (!IsInitialized)
        {
            return;
        }

        SidebarColumn.Width = shellViewModel.IsSidebarCollapsed
            ? new GridLength(88)
            : new GridLength(240);
        ThirdBarColumn.Width = shellViewModel.ThirdBarNavigation.Count == 0
            ? new GridLength(0)
            : new GridLength(220);
        ProfileMenu.Visibility = profileMenuOpen ? Visibility.Visible : Visibility.Collapsed;
        WorkSessionWarningPanel.Visibility = shellViewModel.HasPendingWorkSessionCloseRequest
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateProfileActionAvailability();
        RenderPluginPageHost();
        UpdateProfileAccountText();
    }

    private void UpdateProfileAccountText()
    {
        if (ProfileAccountText is null)
        {
            return;
        }

        ProfileAccountText.Text = session is null
            ? shellViewModel.ProfileAnonymousLabel
            : session.UserId;
        SettingsAccountProfileText.Text = ProfileAccountText.Text;
    }

    private void UpdateProfileActionAvailability()
    {
        var canChangePassword = actionOverrides.ChangePassword is not null ||
            loginRuntime is not null && session is not null;
        var canLock = actionOverrides.ManualLock is not null ||
            sessionRuntime is not null && session is not null;
        var canLogout = actionOverrides.Logout is not null ||
            sessionRuntime is not null && session is not null;
        var canExportDiagnostics = actionOverrides.ExportDiagnostics is not null ||
            loginRuntime is not null && session is not null;

        ChangePasswordButton.IsEnabled = canChangePassword;
        SettingsChangePasswordButton.IsEnabled = canChangePassword;
        CurrentPasswordBox.IsEnabled = canChangePassword;
        NewPasswordBox.IsEnabled = canChangePassword;
        ConfirmNewPasswordBox.IsEnabled = canChangePassword;
        SettingsCurrentPasswordBox.IsEnabled = canChangePassword;
        SettingsNewPasswordBox.IsEnabled = canChangePassword;
        SettingsConfirmNewPasswordBox.IsEnabled = canChangePassword;
        PasswordChangeRememberMeCheckBox.IsEnabled = canChangePassword;
        SettingsPasswordChangeRememberMeCheckBox.IsEnabled = canChangePassword;
        ManualLockButton.IsEnabled = canLock;
        LogoutButton.IsEnabled = canLogout;
        DiagnosticsExportButton.IsEnabled = canExportDiagnostics;
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
        if (previous != long.MinValue && now - previous < SessionActivityWriteIntervalMilliseconds)
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
            .ContinueWith(_ => Interlocked.Exchange(ref sessionActivityRecordInFlight, 0), TaskScheduler.Default);
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

    private void ShowAccountStatus(string statusKey)
    {
        shellViewModel.SetAccountStatusMessage(statusKey);
    }

    private void ShowActionFeedback(string statusKey, string? notificationKey = null)
    {
        shellViewModel.SetStatus(statusKey);
        shellViewModel.SetFeedbackMessage(statusKey);
        shellViewModel.SetAccountStatusMessage(statusKey);
        shellViewModel.SetNotificationMessage(notificationKey);
    }

    private static bool IsDenied(ErrorCode? errorCode)
    {
        return errorCode is { } code &&
            (code.Equals(ErrorCode.Unauthorized) ||
                code.Equals(ErrorCode.Forbidden) ||
                code.Equals(ErrorCode.LicenseDenied) ||
                code.Equals(ErrorCode.LicenseReadOnly));
    }

    private async void Navigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string routeKey })
        {
            return;
        }

        await NavigateToRouteAsync(routeKey).ConfigureAwait(false);
    }

    private async Task NavigateToRouteAsync(string routeKey)
    {
        var pending = workSessionCloseCoordinator?.RequestNavigation(shellViewModel.ActiveRouteKey, routeKey);
        if (pending is not null)
        {
            if (!shellViewModel.SetPendingWorkSessionCloseRequest(pending))
            {
                return;
            }

            shellViewModel.SetFeedbackMessage(HostResourceKeys.ShellFeedbackConfirmation);
            shellViewModel.SetNotificationMessage(HostResourceKeys.ShellNotificationBanner);
            UpdateShellVisualState();
            return;
        }

        if (shellRouteHost is null)
        {
            ConfigureShellRouteHost();
        }

        var result = await shellRouteHost!.ActivateOrHomeAsync(routeKey).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            shellViewModel.SetFeedbackMessage(HostResourceKeys.ShellFeedbackError);
            shellViewModel.SetNotificationMessage(HostResourceKeys.ShellNotificationBanner);
            ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
            return;
        }

        shellViewModel.SetStatus(HostResourceKeys.ShellStatusReady);
        shellViewModel.SetFeedbackMessage(null);
        shellViewModel.SetNotificationMessage(null);
        profileMenuOpen = false;
        QueueShellPreferencePersist();
        RefreshWorkSessions();
        UpdateShellVisualState();
    }

    private void SidebarNodeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string nodeId })
        {
            return;
        }

        shellViewModel.ToggleSidebarNode(nodeId);
        QueueShellPreferencePersist();
    }

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        shellViewModel.ToggleSidebarCollapsed();
        QueueShellPreferencePersist();
        UpdateShellVisualState();
    }

    private void PluginsManagerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedValue: string pluginId })
        {
            return;
        }

        shellViewModel.SelectPlugin(pluginId);
    }

    private async void LicenseImport_Click(object sender, RoutedEventArgs e)
    {
        if (licenseImportAction is null || string.IsNullOrWhiteSpace(session?.UserId))
        {
            shellViewModel.SetLicenseImportFeedback(HostResourceKeys.LicenseImportPermissionDenied);
            return;
        }

        var operation = licenseImportAction.ImportAsync(
            session.UserId,
            CorrelationId.New(),
            windowLifetime.Token);
        TrackUiOperation(operation);
        await operation;
    }

    private void LicenseImportCancel_Click(object sender, RoutedEventArgs e)
    {
        loginRuntime?.LicenseImportCoordinator.CancelLicenseImport();
    }

    private async void PluginsManagerInstall_Click(object sender, RoutedEventArgs e)
    {
        var usesOverride = actionOverrides.InstallPluginPackageAsync is not null ||
            actionOverrides.InstallPluginPackage is not null;
        var packagePath = usesOverride
            ? "override.dobbyplugin"
            : new WindowsPluginPackageFilePicker().PickPluginPackageFile();
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            shellViewModel.SetPluginsManagerFeedback(null);
            return;
        }

        var operation = EnsurePluginManagerOperationCoordinator().InstallAsync(
            packagePath,
            windowLifetime.Token);
        TrackUiOperation(operation);
        await operation;
    }

    private void PluginsManagerCancel_Click(object sender, RoutedEventArgs e)
    {
        EnsurePluginManagerOperationCoordinator().CancelActiveOperation();
    }

    private async void PluginsManagerEnable_Click(object sender, RoutedEventArgs e)
    {
        await RunSelectedPluginLifecycleActionAsync(
            (coordinator, pluginId, token) => coordinator.EnableAsync(pluginId, token));
    }

    private async void PluginsManagerDisable_Click(object sender, RoutedEventArgs e)
    {
        var selected = shellViewModel.PluginsManagerSelectedItem;
        if (selected is null)
        {
            shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
            return;
        }

        var pending = workSessionCloseCoordinator?.RequestPluginDisable(selected.PluginId);
        if (pending is not null)
        {
            if (!shellViewModel.SetPendingWorkSessionCloseRequest(pending))
            {
                return;
            }

            shellViewModel.SetFeedbackMessage(HostResourceKeys.ShellFeedbackConfirmation);
            shellViewModel.SetNotificationMessage(HostResourceKeys.ShellNotificationBanner);
            UpdateShellVisualState();
            return;
        }

        await RunPluginDisableAsync(selected.PluginId);
    }

    private Task<Result> RunPluginDisableAsync(string pluginId)
    {
        return RunPluginLifecycleActionAsync(
            pluginId,
            (coordinator, targetPluginId, token) => coordinator.DisableAsync(targetPluginId, token));
    }

    private async void PluginsManagerUninstall_Click(object sender, RoutedEventArgs e)
    {
        var selected = shellViewModel.PluginsManagerSelectedItem;
        if (selected is null)
        {
            shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
            return;
        }

        if (shellViewModel.HasPendingWorkSessionCloseRequest)
        {
            return;
        }

        var confirmation = ConfirmPluginUninstall(selected);
        if (!confirmation.Confirmed)
        {
            shellViewModel.SetPluginsManagerFeedback(null);
            return;
        }

        var pending = workSessionCloseCoordinator?.RequestPluginUninstall(selected.PluginId);
        if (pending is not null)
        {
            if (!shellViewModel.SetPendingWorkSessionCloseRequest(pending))
            {
                return;
            }

            pendingConfirmedUninstallRequestId = pending.RequestId;
            pendingConfirmedUninstallDataChoice = confirmation.DataChoice;

            shellViewModel.SetFeedbackMessage(HostResourceKeys.ShellFeedbackConfirmation);
            shellViewModel.SetNotificationMessage(HostResourceKeys.ShellNotificationBanner);
            UpdateShellVisualState();
            return;
        }

        await RunPluginUninstallAsync(selected.PluginId, confirmation.DataChoice);
    }

    private PluginUninstallConfirmationResult ConfirmPluginUninstall(ShellPluginManagerItem plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        var wasActivatedInCurrentProcess = actionOverrides.WasPluginActivatedInCurrentProcess?.Invoke(plugin.PluginId) ??
            loginRuntime?.PluginLifecycleCoordinator.WasActivatedInCurrentProcess(plugin.PluginId) ??
            false;
        var request = new PluginUninstallConfirmationRequest(
            plugin.PluginId,
            plugin.DisplayName,
            wasActivatedInCurrentProcess);
        return actionOverrides.ConfirmPluginUninstall?.Invoke(request) ??
            new WpfPluginUninstallConfirmation(this).Confirm(request);
    }

    private Task<Result> RunPluginUninstallAsync(string pluginId, PluginUninstallDataChoice dataChoice)
    {
        return RunPluginLifecycleActionAsync(
            pluginId,
            (coordinator, targetPluginId, token) => coordinator.UninstallAsync(targetPluginId, dataChoice, token));
    }

    private Task<Result> RunSelectedPluginLifecycleActionAsync(
        Func<ShellPluginManagerOperationCoordinator, string, CancellationToken, Task<Result>> action)
    {
        var selected = shellViewModel.PluginsManagerSelectedItem;
        if (selected is null)
        {
            shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
            return Task.FromResult(Result.Failure(ErrorCode.InvalidInput));
        }

        return RunPluginLifecycleActionAsync(selected.PluginId, action);
    }

    private Task<Result> RunPluginLifecycleActionAsync(
        string pluginId,
        Func<ShellPluginManagerOperationCoordinator, string, CancellationToken, Task<Result>> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        var operation = action(
            EnsurePluginManagerOperationCoordinator(),
            pluginId,
            windowLifetime.Token);
        TrackUiOperation(operation);
        return operation;
    }

    private ShellPluginManagerOperationCoordinator EnsurePluginManagerOperationCoordinator()
    {
        return pluginManagerOperationCoordinator ??=
            CreatePluginManagerOperationCoordinator(windowLifetime.Token);
    }

    private ShellPluginManagerOperationCoordinator CreatePluginManagerOperationCoordinator(
        CancellationToken authenticatedLifetime)
    {
        var actions = new ShellPluginManagerRuntimeActions(
            InstallPluginPackageAsync,
            EnablePluginAsync,
            DisablePluginAsync,
            UninstallPluginAsync);
        return new ShellPluginManagerOperationCoordinator(
            shellViewModel,
            loginRuntime?.BackgroundOperations,
            actions,
            RefreshPluginsManagerSurfaceFromRuntime,
            authenticatedLifetime);
    }

    private Task<Result<InstalledPluginState>> InstallPluginPackageAsync(
        string packagePath,
        CorrelationId correlationId,
        CancellationToken cancellationToken,
        IProgress<PluginPackageInstallProgress> progress)
    {
        if (actionOverrides.InstallPluginPackageAsync is not null)
        {
            return actionOverrides.InstallPluginPackageAsync(correlationId, cancellationToken);
        }
        if (actionOverrides.InstallPluginPackage is not null)
        {
            return RunLegacyOverrideAsync(
                () => actionOverrides.InstallPluginPackage(correlationId),
                cancellationToken);
        }

        return loginRuntime is not null && session is not null
            ? loginRuntime.PluginPackageInstallCoordinator.InstallPackageAsync(
                session.UserId,
                packagePath,
                new AssemblyHostVersionProvider(typeof(MainWindow).Assembly).Current,
                correlationId,
                cancellationToken,
                progress)
            : Task.FromResult(Result.Failure<InstalledPluginState>(ErrorCode.Forbidden));
    }

    private Task<Result> EnablePluginAsync(
        string pluginId,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        if (actionOverrides.EnablePluginAsync is not null)
        {
            return actionOverrides.EnablePluginAsync(pluginId, correlationId, cancellationToken);
        }
        if (actionOverrides.EnablePlugin is not null)
        {
            return RunLegacyOverrideAsync(
                () => actionOverrides.EnablePlugin(pluginId, correlationId),
                cancellationToken);
        }

        return loginRuntime is not null && session is not null
            ? loginRuntime.PluginLifecycleCoordinator.EnablePluginAsync(
                session.UserId,
                pluginId,
                correlationId,
                cancellationToken)
            : Task.FromResult(Result.Failure(ErrorCode.Forbidden));
    }

    private Task<Result> DisablePluginAsync(
        string pluginId,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        if (actionOverrides.DisablePluginAsync is not null)
        {
            return actionOverrides.DisablePluginAsync(pluginId, correlationId, cancellationToken);
        }
        if (actionOverrides.DisablePlugin is not null)
        {
            return RunLegacyOverrideAsync(
                () => actionOverrides.DisablePlugin(pluginId, correlationId),
                cancellationToken);
        }

        return loginRuntime is not null && session is not null
            ? loginRuntime.PluginLifecycleCoordinator.DisablePluginAsync(
                session.UserId,
                pluginId,
                correlationId,
                cancellationToken)
            : Task.FromResult(Result.Failure(ErrorCode.Forbidden));
    }

    private Task<Result> UninstallPluginAsync(
        string pluginId,
        PluginUninstallDataChoice dataChoice,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        if (actionOverrides.UninstallPluginAsync is not null)
        {
            return actionOverrides.UninstallPluginAsync(
                pluginId,
                dataChoice,
                correlationId,
                cancellationToken);
        }
        if (actionOverrides.UninstallPlugin is not null)
        {
            return RunLegacyOverrideAsync(
                () => actionOverrides.UninstallPlugin(pluginId, dataChoice, correlationId),
                cancellationToken);
        }

        return loginRuntime is not null && session is not null
            ? loginRuntime.PluginLifecycleCoordinator.UninstallPluginAsync(
                session.UserId,
                pluginId,
                dataChoice,
                correlationId,
                cancellationToken)
            : Task.FromResult(Result.Failure(ErrorCode.Forbidden));
    }

    private static Task<Result<T>> RunLegacyOverrideAsync<T>(
        Func<Result<T>> action,
        CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return action();
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    private static Task<Result> RunLegacyOverrideAsync(
        Func<Result> action,
        CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return action();
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    private void TrackUiOperation(Task operation)
    {
        lock (uiOperationGate)
        {
            activeUiOperations.Add(operation);
        }

        _ = operation.ContinueWith(
            completed =>
            {
                lock (uiOperationGate)
                {
                    activeUiOperations.Remove(completed);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal async Task ShutdownOperationsAsync()
    {
        windowLifetime.Cancel();
        loginRuntime?.LicenseImportCoordinator.CancelLicenseImport();
        if (pluginManagerOperationCoordinator is not null)
        {
            await pluginManagerOperationCoordinator.CancelAndDrainAsync().ConfigureAwait(false);
        }

        Task[] operations;
        lock (uiOperationGate)
        {
            operations = activeUiOperations.ToArray();
        }

        try
        {
            await Task.WhenAll(operations).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RefreshPluginsManagerSurfaceFromRuntime()
    {
        var runtime = loginRuntime;
        if (runtime is null)
        {
            return;
        }

        var snapshot = runtime.PluginRuntimeCoordinator.MetadataCache.Snapshot();
        if (!snapshot.IsSuccess)
        {
            shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.ShellFeedbackError);
            return;
        }

        shellViewModel.SetPluginsManagerSurface(
            shellViewModel.PluginsManagerCanView,
            shellViewModel.PluginsManagerCanImport,
            shellViewModel.PluginsManagerCanConfigure,
            shellViewModel.PluginsManagerCanDelete,
            snapshot.Value.Select(ToShellPluginManagerItem).ToArray());
    }

    private static ShellPluginManagerItem ToShellPluginManagerItem(InstalledPluginMetadata plugin)
    {
        return new ShellPluginManagerItem(
            plugin.PluginId,
            plugin.DisplayName,
            plugin.Version,
            plugin.Description,
            plugin.IsEnabled,
            plugin.StatusSummary,
            plugin.IsPendingUninstall,
            plugin.RestartRequired,
            TrustState: ShellPluginTrustState.Verified,
            LicenseSummary: string.Empty,
            FaultSummary: string.Empty,
            SafeRejectionText: SafePluginRejectionText(plugin.StatusSummary));
    }

    private static string SafePluginRejectionText(string statusSummary)
    {
        return statusSummary.Contains("rejected", StringComparison.OrdinalIgnoreCase) ||
            statusSummary.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            ? statusSummary
            : string.Empty;
    }

    private async ValueTask RefreshLicenseModeAfterImport(string pluginId)
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(() => RefreshLicenseMode(pluginId));
            return;
        }

        RefreshLicenseMode(pluginId);
    }

    private void RefreshLicenseMode(string pluginId)
    {
        var runtime = loginRuntime;
        if (runtime is null)
        {
            return;
        }

        var installedSnapshot = runtime.PluginRuntimeCoordinator.StateStore.Snapshot();
        if (!installedSnapshot.IsSuccess)
        {
            return;
        }

        var installed = installedSnapshot.Value.SingleOrDefault(plugin =>
            plugin.PluginId.Equals(pluginId, StringComparison.Ordinal));
        if (installed is null)
        {
            return;
        }

        var mode = runtime.LicenseRuntimeModeProvider.GetCurrentMode(
            installed.PluginId,
            installed.PackageFingerprint);
        if (mode.IsSuccess)
        {
            shellViewModel.SetLicenseRuntimeMode(mode.Value);
        }
    }

    private void WorkspaceNewTab_Click(object sender, RoutedEventArgs e)
    {
        shellViewModel.OpenWorkspaceTab();
        QueueShellPreferencePersist();
    }

    private async void WorkspaceTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid tabId })
        {
            return;
        }

        var previousActiveTab = shellViewModel.ActiveWorkspaceTab;
        var restoredRoute = shellViewModel.SelectWorkspaceTabForRouteActivation(tabId);
        if (!restoredRoute.IsSuccess)
        {
            ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
            return;
        }

        if (shellRouteHost is null)
        {
            ConfigureShellRouteHost();
        }

        var restoredRouteKey = restoredRoute.Value;
        var activateTab = shellViewModel.ActivateWorkspaceTab(tabId, restoredRouteKey);
        if (!activateTab.IsSuccess)
        {
            ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
            return;
        }

        var result = await shellRouteHost!.ActivateOrHomeAsync(restoredRouteKey).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            shellViewModel.ActivateWorkspaceTab(previousActiveTab.TabId, previousActiveTab.RouteKey);
            ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
            return;
        }

        if (!result.Value.Equals(restoredRouteKey, StringComparison.Ordinal))
        {
            var normalizeTab = shellViewModel.ActivateWorkspaceTab(tabId, result.Value);
            if (!normalizeTab.IsSuccess)
            {
                shellViewModel.ActivateWorkspaceTab(previousActiveTab.TabId, previousActiveTab.RouteKey);
                ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
                return;
            }
        }

        QueueShellPreferencePersist();
        RefreshWorkSessions();
        UpdateShellVisualState();
    }

    private async void WorkspaceTabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid tabId })
        {
            return;
        }

        var continuationRoute = shellViewModel.CloseWorkspaceTab(tabId);
        if (!continuationRoute.IsSuccess)
        {
            ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
            return;
        }

        if (!string.IsNullOrWhiteSpace(continuationRoute.Value))
        {
            if (shellRouteHost is null)
            {
                ConfigureShellRouteHost();
            }

            var activation = await shellRouteHost!.ActivateOrHomeAsync(continuationRoute.Value).ConfigureAwait(true);
            if (!activation.IsSuccess)
            {
                ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
                return;
            }

            var commitClose = shellViewModel.CommitCloseWorkspaceTab(tabId, activation.Value);
            if (!commitClose.IsSuccess)
            {
                ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
                return;
            }
        }

        QueueShellPreferencePersist();
        RefreshWorkSessions();
        UpdateShellVisualState();
    }

    private void ProfileMenuButton_Click(object sender, RoutedEventArgs e)
    {
        profileMenuOpen = !profileMenuOpen;
        UpdateShellVisualState();
    }

    private async void ProfileAccountInfo_Click(object sender, RoutedEventArgs e)
    {
        await NavigateToRouteAsync(RouteRegistry.SettingsRouteKey).ConfigureAwait(false);
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        ChangePasswordCore(
            CurrentPasswordBox,
            NewPasswordBox,
            ConfirmNewPasswordBox,
            PasswordChangeRememberMeCheckBox);
    }

    private void SettingsChangePassword_Click(object sender, RoutedEventArgs e)
    {
        ChangePasswordCore(
            SettingsCurrentPasswordBox,
            SettingsNewPasswordBox,
            SettingsConfirmNewPasswordBox,
            SettingsPasswordChangeRememberMeCheckBox);
    }

    private void ChangePasswordCore(
        PasswordBox currentPasswordBox,
        PasswordBox newPasswordBox,
        PasswordBox confirmationBox,
        CheckBox rememberMeCheckBox)
    {
        var currentPassword = currentPasswordBox.Password;
        var newPassword = newPasswordBox.Password;
        var confirmation = confirmationBox.Password;
        if (string.IsNullOrWhiteSpace(currentPassword) ||
            string.IsNullOrWhiteSpace(newPassword) ||
            !StringComparer.Ordinal.Equals(newPassword, confirmation))
        {
            ShowActionFeedback(HostResourceKeys.ShellProfilePasswordChangeError, HostResourceKeys.ShellNotificationBanner);
            return;
        }

        ShowActionFeedback(HostResourceKeys.ShellProfilePasswordChangeBusy);
        Result result;
        var correlationId = CorrelationId.New();
        if (actionOverrides.ChangePassword is not null)
        {
            result = actionOverrides.ChangePassword(correlationId);
        }
        else if (loginRuntime is not null && session is not null)
        {
            result = loginRuntime.AccountSettingsModel.ChangePassword(
                session.UserId,
                session.SessionId,
                currentPassword,
                newPassword,
                confirmation,
                rememberMeCheckBox.IsChecked == true,
                correlationId);
        }
        else
        {
            result = Result.Failure(ErrorCode.UnsupportedOperation);
        }

        if (!result.IsSuccess)
        {
            currentPasswordBox.Password = string.Empty;
            var feedbackKey = IsDenied(result.ErrorCode)
                ? HostResourceKeys.ShellProfilePasswordChangeDenied
                : HostResourceKeys.ShellProfilePasswordChangeError;
            ShowActionFeedback(feedbackKey, HostResourceKeys.ShellNotificationBanner);
            return;
        }

        currentPasswordBox.Password = string.Empty;
        newPasswordBox.Password = string.Empty;
        confirmationBox.Password = string.Empty;
        ShowActionFeedback(HostResourceKeys.ShellProfilePasswordChangeSuccess, HostResourceKeys.ShellNotificationToast);
    }

    private void ManualLock_Click(object sender, RoutedEventArgs e)
    {
        ShowActionFeedback(HostResourceKeys.ShellProfileManualLockBusy);
        Result result;
        var correlationId = CorrelationId.New();
        if (actionOverrides.ManualLock is not null)
        {
            result = actionOverrides.ManualLock(correlationId);
        }
        else if (sessionRuntime is not null && session is not null)
        {
            result = sessionRuntime.LockNow(session, correlationId);
        }
        else
        {
            result = Result.Failure(ErrorCode.UnsupportedOperation);
        }

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == ErrorCode.Cancelled)
            {
                ReturnToLoginRequested = true;
                Close();
                return;
            }

            var feedbackKey = IsDenied(result.ErrorCode)
                ? HostResourceKeys.ShellProfileManualLockDenied
                : HostResourceKeys.ShellProfileManualLockError;
            ShowActionFeedback(feedbackKey, HostResourceKeys.ShellNotificationBanner);
            return;
        }

        ShowActionFeedback(HostResourceKeys.ShellProfileManualLockSuccess, HostResourceKeys.ShellNotificationToast);
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var pending = workSessionCloseCoordinator?.HandleLogout();
        if (pending is not null)
        {
            if (!shellViewModel.SetPendingWorkSessionCloseRequest(pending))
            {
                return;
            }

            shellViewModel.SetFeedbackMessage(HostResourceKeys.ShellFeedbackConfirmation);
            shellViewModel.SetNotificationMessage(HostResourceKeys.ShellNotificationBanner);
            UpdateShellVisualState();
            return;
        }

        PerformLogout();
    }

    private async void WorkSessionStay_Click(object sender, RoutedEventArgs e)
    {
        await ResolvePendingWorkSessionChoiceAsync(WorkSessionCloseChoice.Cancel).ConfigureAwait(false);
    }

    private async void WorkSessionMinimize_Click(object sender, RoutedEventArgs e)
    {
        await ResolvePendingWorkSessionChoiceAsync(WorkSessionCloseChoice.Minimize).ConfigureAwait(false);
    }

    private async void WorkSessionClose_Click(object sender, RoutedEventArgs e)
    {
        await ResolvePendingWorkSessionChoiceAsync(WorkSessionCloseChoice.Close).ConfigureAwait(false);
    }

    private async Task ResolvePendingWorkSessionChoiceAsync(WorkSessionCloseChoice choice)
    {
        if (workSessionCloseCoordinator is null)
        {
            return;
        }

        var pending = shellViewModel.PendingWorkSessionCloseRequest;
        if (pending is null)
        {
            return;
        }

        var continuationRoute = shellViewModel.ResolvePendingWorkSessionClose(
            choice,
            workSessionCloseCoordinator,
            pending);
        if (!continuationRoute.IsSuccess)
        {
            shellViewModel.SetFeedbackMessage(HostResourceKeys.ShellFeedbackError);
            ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
            return;
        }

        if (!string.IsNullOrWhiteSpace(continuationRoute.Value))
        {
            if (shellRouteHost is null)
            {
                ConfigureShellRouteHost();
            }

            var activation = await shellRouteHost!.ActivateOrHomeAsync(continuationRoute.Value).ConfigureAwait(true);
            if (!activation.IsSuccess)
            {
                shellViewModel.SetFeedbackMessage(HostResourceKeys.ShellFeedbackError);
                ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
                return;
            }

            var commitClose = shellViewModel.CommitPendingWorkSessionClose(
                choice,
                workSessionCloseCoordinator,
                pending);
            if (!commitClose.IsSuccess)
            {
                shellViewModel.SetFeedbackMessage(HostResourceKeys.ShellFeedbackError);
                ShowAccountStatus(HostResourceKeys.ShellFeedbackError);
                return;
            }
        }

        if (pending.Action == PendingWorkSessionAction.PluginUninstall &&
            choice != WorkSessionCloseChoice.Close)
        {
            ClearPendingUninstallConfirmation(pending);
        }

        if (choice == WorkSessionCloseChoice.Close &&
            RequestRemainingWorkSessionClose(pending))
        {
            return;
        }

        shellViewModel.SetFeedbackMessage(null);
        shellViewModel.SetNotificationMessage(null);
        RefreshWorkSessions();
        UpdateShellVisualState();

        if (choice == WorkSessionCloseChoice.Close && pending is not null)
        {
            await ContinuePendingPluginLifecycleActionAsync(pending);
        }

        if (pending?.Action == PendingWorkSessionAction.Logout && choice == WorkSessionCloseChoice.Close)
        {
            PerformLogout();
        }
    }

    private async Task ContinuePendingPluginLifecycleActionAsync(PendingWorkSessionCloseRequest pending)
    {
        if (string.IsNullOrWhiteSpace(pending.ActionTargetId))
        {
            if (pending.Action is PendingWorkSessionAction.PluginDisable or PendingWorkSessionAction.PluginUninstall)
            {
                shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
            }

            return;
        }

        switch (pending.Action)
        {
            case PendingWorkSessionAction.PluginDisable:
                await RunPluginDisableAsync(pending.ActionTargetId);
                break;
            case PendingWorkSessionAction.PluginUninstall:
                if (pendingConfirmedUninstallRequestId != pending.RequestId ||
                    pendingConfirmedUninstallDataChoice is not { } dataChoice)
                {
                    ClearPendingUninstallConfirmation(pending);
                    shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
                    return;
                }

                ClearPendingUninstallConfirmation(pending);
                await RunPluginUninstallAsync(pending.ActionTargetId, dataChoice);
                break;
        }
    }

    private bool RequestRemainingWorkSessionClose(PendingWorkSessionCloseRequest completedRequest)
    {
        if (workSessionCloseCoordinator is null)
        {
            return false;
        }

        var remaining = completedRequest.Action switch
        {
            PendingWorkSessionAction.Logout => workSessionCloseCoordinator.RequestLogout(),
            PendingWorkSessionAction.PluginDisable when !string.IsNullOrWhiteSpace(completedRequest.ActionTargetId) =>
                workSessionCloseCoordinator.RequestPluginDisable(completedRequest.ActionTargetId),
            PendingWorkSessionAction.PluginUninstall when !string.IsNullOrWhiteSpace(completedRequest.ActionTargetId) =>
                workSessionCloseCoordinator.RequestPluginUninstall(completedRequest.ActionTargetId),
            _ => null
        };
        if (remaining is null)
        {
            return false;
        }

        if (completedRequest.Action == PendingWorkSessionAction.PluginUninstall)
        {
            if (pendingConfirmedUninstallRequestId != completedRequest.RequestId ||
                pendingConfirmedUninstallDataChoice is null)
            {
                ClearPendingUninstallConfirmation(completedRequest);
                shellViewModel.SetPluginsManagerFeedback(HostResourceKeys.PluginsManagerOperationDenied);
                return true;
            }

        }

        if (!shellViewModel.SetPendingWorkSessionCloseRequest(remaining))
        {
            ClearPendingUninstallConfirmation(completedRequest);
            shellViewModel.SetFeedbackMessage(HostResourceKeys.ShellFeedbackError);
            return true;
        }

        if (completedRequest.Action == PendingWorkSessionAction.PluginUninstall)
        {
            pendingConfirmedUninstallRequestId = remaining.RequestId;
        }

        shellViewModel.SetFeedbackMessage(HostResourceKeys.ShellFeedbackConfirmation);
        shellViewModel.SetNotificationMessage(HostResourceKeys.ShellNotificationBanner);
        RefreshWorkSessions();
        UpdateShellVisualState();
        return true;
    }

    private void ClearPendingUninstallConfirmation(PendingWorkSessionCloseRequest pending)
    {
        if (pendingConfirmedUninstallRequestId != pending.RequestId)
        {
            return;
        }

        pendingConfirmedUninstallRequestId = null;
        pendingConfirmedUninstallDataChoice = null;
    }

    private void DiagnosticsExport_Click(object sender, RoutedEventArgs e)
    {
        ShowActionFeedback(HostResourceKeys.ShellDiagnosticsExportBusy);
        var correlationId = CorrelationId.New();
        Result<DiagnosticsExportPackage> result;
        if (actionOverrides.ExportDiagnostics is not null)
        {
            result = actionOverrides.ExportDiagnostics(correlationId);
        }
        else if (loginRuntime is not null && session is not null)
        {
            result = loginRuntime.DiagnosticsExportCoordinator.Export(
                session.UserId,
                loginRuntime.DiagnosticsSourceDirectory,
                loginRuntime.DiagnosticsOutputZipPath,
                correlationId);
        }
        else
        {
            result = Result.Failure<DiagnosticsExportPackage>(ErrorCode.UnsupportedOperation);
        }

        if (!result.IsSuccess)
        {
            var feedbackKey = IsDenied(result.ErrorCode)
                ? HostResourceKeys.ShellDiagnosticsExportDenied
                : HostResourceKeys.ShellDiagnosticsExportError;
            ShowActionFeedback(feedbackKey, HostResourceKeys.ShellNotificationBanner);
            return;
        }

        ShowActionFeedback(HostResourceKeys.ShellDiagnosticsExportSuccess, HostResourceKeys.ShellNotificationToast);
    }

    private void RestoreWorkSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid sessionId })
        {
            return;
        }

        actionOverrides.RestoreWorkSession?.Invoke(sessionId);
        workSessionRegistry?.Restore(sessionId);
        RefreshWorkSessions();
    }

    private void RefreshWorkSessions()
    {
        IReadOnlyList<WorkSession> sessions;
        if (actionOverrides.SnapshotWorkSessions is not null)
        {
            sessions = actionOverrides.SnapshotWorkSessions();
        }
        else if (workSessionRegistry is not null)
        {
            sessions = workSessionRegistry.Snapshot();
        }
        else
        {
            sessions = [];
        }

        shellViewModel.SetMinimizedWorkSessions(sessions);
        UpdateShellVisualState();
    }

    private void RenderPluginPageHost()
    {
        if (PluginPageHost is null)
        {
            return;
        }

        var model = shellViewModel.ActivePluginPageModel;
        if (model is null)
        {
            PluginPageHost.Content = null;
            return;
        }

        var render = pluginPageRenderRegistry.Render(model);
        PluginPageHost.Content = render.IsSuccess
            ? render.Value
            : new TextBlock
            {
                Text = shellViewModel.PluginPageUnavailableText,
                TextWrapping = TextWrapping.Wrap
            };
    }

    private void WindowMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void WindowMaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowMaximizeRestore();
    }

    private void WindowTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        var nextTheme = Equals(shellViewModel.Layout.Theme, AppThemePreference.Dark)
            ? AppThemePreference.Light
            : AppThemePreference.Dark;
        ApplyThemePreference(nextTheme);
    }

    private void AppearanceThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized ||
            System.Windows.Application.Current is null ||
            sender is not ComboBox { SelectedValue: string selectedThemeValue } ||
            !Enum.TryParse<AppThemePreference>(selectedThemeValue, ignoreCase: false, out var selectedTheme) ||
            Equals(shellViewModel.Layout.Theme, selectedTheme))
        {
            return;
        }

        ApplyThemePreference(selectedTheme);
    }

    private void ApplyThemePreference(AppThemePreference nextTheme)
    {
        shellViewModel.SetTheme(nextTheme);
        ShellThemeCoordinator.ApplyTheme(System.Windows.Application.Current, nextTheme);
        StartupThemeSnapshotStore.SaveTheme(nextTheme);
        QueueThemePreferencePersist(nextTheme);
    }

    private void SecurityAutoLockSave_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(SecurityAutoLockMinutesInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
        {
            ShowActionFeedback(HostResourceKeys.ShellFeedbackError, HostResourceKeys.ShellNotificationBanner);
            return;
        }

        Result result;
        if (preferenceSource is null)
        {
            result = Result.Failure(ErrorCode.UnsupportedOperation);
        }
        else
        {
            result = new SecuritySessionSettingsModel(preferenceSource)
                .SetAutoLock(SecurityAutoLockEnabledCheckBox.IsChecked == true, minutes);
        }

        if (!result.IsSuccess)
        {
            ShowActionFeedback(HostResourceKeys.ShellFeedbackError, HostResourceKeys.ShellNotificationBanner);
            return;
        }

        shellViewModel.SetSettingsAndAbout(
            shellViewModel.SettingsCategories,
            shellViewModel.AboutFields,
            SecurityAutoLockEnabledCheckBox.IsChecked == true,
            minutes);
        ShowActionFeedback(HostResourceKeys.ShellFeedbackSuccess, HostResourceKeys.ShellNotificationToast);
    }

    private void AboutCopyDeviceCode_Click(object sender, RoutedEventArgs e)
    {
        var deviceCode = shellViewModel.AboutDeviceCode;
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            ShowActionFeedback(HostResourceKeys.AboutDeviceCodeCopyError, HostResourceKeys.ShellNotificationBanner);
            return;
        }

        try
        {
            Clipboard.SetText(deviceCode);
            ShowActionFeedback(HostResourceKeys.AboutDeviceCodeCopied, HostResourceKeys.ShellNotificationToast);
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
        {
            ShowActionFeedback(HostResourceKeys.AboutDeviceCodeCopyError, HostResourceKeys.ShellNotificationBanner);
        }
    }

    private void PerformLogout()
    {
        ShowActionFeedback(HostResourceKeys.ShellProfileLogoutBusy);
        Result result;
        var correlationId = CorrelationId.New();
        if (actionOverrides.Logout is not null)
        {
            result = actionOverrides.Logout(correlationId);
        }
        else if (sessionRuntime is not null && session is not null)
        {
            result = sessionRuntime.Logout(session, correlationId);
        }
        else
        {
            result = Result.Failure(ErrorCode.UnsupportedOperation);
        }

        if (!result.IsSuccess)
        {
            var feedbackKey = IsDenied(result.ErrorCode)
                ? HostResourceKeys.ShellProfileLogoutDenied
                : HostResourceKeys.ShellProfileLogoutError;
            ShowActionFeedback(feedbackKey, HostResourceKeys.ShellNotificationBanner);
            return;
        }

        ShowActionFeedback(HostResourceKeys.ShellProfileLogoutSuccess, HostResourceKeys.ShellNotificationToast);
        ReturnToLoginRequested = true;
        BeginApplicationExit();
        Close();
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

            _ = Dispatcher.InvokeAsync(() => ShowAccountStatus(HostResourceKeys.ShellFeedbackError));
        });
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
        }
        else
        {
            ApplyWindowPlacement(runtimePreference.WindowPlacement);
        }

        UpdateShellVisualState();
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

    private static void PersistShellPreference(ISettingsPreferenceSource preferenceSource, ShellLayoutPreference runtimePreference)
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
}

public sealed class ShellActionOverrides
{
    public Func<CorrelationId, Result>? ChangePassword { get; init; }

    public Func<CorrelationId, Result>? ManualLock { get; init; }

    public Func<CorrelationId, Result>? Logout { get; init; }

    public Func<CorrelationId, Result<DiagnosticsExportPackage>>? ExportDiagnostics { get; init; }

    public Func<CorrelationId, Result<InstalledPluginState>>? InstallPluginPackage { get; init; }

    public Func<CorrelationId, CancellationToken, Task<Result<InstalledPluginState>>>? InstallPluginPackageAsync { get; init; }

    public Func<string, CorrelationId, Result>? EnablePlugin { get; init; }

    public Func<string, CorrelationId, CancellationToken, Task<Result>>? EnablePluginAsync { get; init; }

    public Func<string, CorrelationId, Result>? DisablePlugin { get; init; }

    public Func<string, CorrelationId, CancellationToken, Task<Result>>? DisablePluginAsync { get; init; }

    public Func<string, PluginUninstallDataChoice, CorrelationId, Result>? UninstallPlugin { get; init; }

    public Func<string, PluginUninstallDataChoice, CorrelationId, CancellationToken, Task<Result>>? UninstallPluginAsync { get; init; }

    public Func<string, bool>? WasPluginActivatedInCurrentProcess { get; init; }

    public Func<PluginUninstallConfirmationRequest, PluginUninstallConfirmationResult>? ConfirmPluginUninstall { get; init; }

    public Func<IReadOnlyList<WorkSession>>? SnapshotWorkSessions { get; init; }

    public Action<Guid>? RestoreWorkSession { get; init; }
}

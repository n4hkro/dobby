using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Diagnostics;
using Dobby.Host.Login;
using Dobby.Security.Authentication;
using Dobby.Shell;
using Dobby.Shell.Layout;

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
    private ISettingsPreferenceSource? preferenceSource;
    private ShellViewModel shellViewModel = null!;
    private ShellThemeCoordinator? themeCoordinator;
    private ShellLayoutPreference? initialPreference;
    private readonly IShellWindowCloseConfirmation shellWindowCloseConfirmation;
    private readonly SemaphoreSlim preferencePersistGate = new(1, 1);
    private int themePreferencePersistVersion;
    private int sessionActivityRecordInFlight;
    private long lastSessionActivityRecordQueuedAtMilliseconds = long.MinValue;
    private DispatcherTimer? sessionTimer;
    private bool forceShellStartupMaximized;
    private bool initialPreferenceExplicitlyProvided;

    public bool ReturnToLoginRequested { get; private set; }

    public StartupMilestone? ShellFirstPaintMilestone { get; private set; }

    public MainWindow()
        : this(CreateDefaultShellViewModel())
    {
    }

    public MainWindow(ShellViewModel initialShellViewModel)
    {
        ArgumentNullException.ThrowIfNull(initialShellViewModel);

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
        ISettingsPreferenceSource? preferenceSource,
        ShellThemeCoordinator? themeCoordinator,
        IShellWindowCloseConfirmation? shellWindowCloseConfirmation = null,
        ShellLayoutPreference? initialPreference = null,
        PostLoginSessionRuntime? sessionRuntime = null,
        SessionRecord? session = null)
        : this(shellViewModel)
    {
        this.sessionRuntime = sessionRuntime;
        this.session = session;
        this.preferenceSource = preferenceSource;
        this.themeCoordinator = themeCoordinator;
        this.initialPreference = initialPreference;
        this.shellWindowCloseConfirmation = shellWindowCloseConfirmation ?? new WpfShellWindowCloseConfirmation(this);
        DataContext = shellViewModel;
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
        ShellLayoutPreference? runtimeInitialPreference)
    {
        ArgumentNullException.ThrowIfNull(loginRuntime);
        ArgumentNullException.ThrowIfNull(scopedPreferenceSource);
        ArgumentNullException.ThrowIfNull(runtimeThemeCoordinator);

        sessionRuntime = loginRuntime.PostLoginSessionRuntime;
        session = authenticatedSession;
        preferenceSource = scopedPreferenceSource;
        themeCoordinator = runtimeThemeCoordinator;
        initialPreference = runtimeInitialPreference;
        initialPreferenceExplicitlyProvided = true;
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

        shellViewModel = nextViewModel;
        DataContext = shellViewModel;
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

    private void ShowAccountStatus(string text)
    {
    }

    private void Navigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string routeKey })
        {
            return;
        }

        var result = shellViewModel.ActivateRoute(routeKey);
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

}

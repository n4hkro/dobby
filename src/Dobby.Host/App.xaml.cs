using System.Windows;
using System.Windows.Threading;
using Dobby.Application;
using Dobby.Host.Login;
using Dobby.Host.Startup;

namespace Dobby.Host;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? singleInstanceCoordinator;
    private IDisposable? singleInstanceActivationListener;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (CrashRecoverySmokeHook.TryRunFromEnvironment(out var smokeExitCode))
        {
            Shutdown(smokeExitCode);
            return;
        }

        var firstLoginStartupTiming = LoginStartupTimingTrace.Start();
        singleInstanceCoordinator = new SingleInstanceCoordinator();
        var bootstrap = HostBootstrapper.BootstrapLoginWindow(new HostLoginWindowBootstrapOptions(
            SingleInstanceCoordinatorFactory: () => singleInstanceCoordinator));
        firstLoginStartupTiming.MarkBootstrapCompleted();
        if (!bootstrap.Milestones.Any(milestone => milestone.Name == Dobby.Diagnostics.StartupMilestoneNames.LoginVisible))
        {
            Shutdown();
            return;
        }

        singleInstanceActivationListener = singleInstanceCoordinator.StartActivationListener(ActivateCurrentWindow);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        await RunLoginLoopAsync(firstLoginStartupTiming);
    }

    private void ActivateCurrentWindow()
    {
        if (Dispatcher.HasShutdownStarted)
        {
            return;
        }

        try
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                var window = MainWindow;
                if (window is null)
                {
                    return;
                }

                WindowActivationHelper.Activate(window);
            }));
        }
        catch (InvalidOperationException) when (Dispatcher.HasShutdownStarted)
        {
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task RunLoginLoopAsync(LoginStartupTimingTrace? firstLoginStartupTiming)
    {
        while (true)
        {
            var startupTiming = firstLoginStartupTiming ?? LoginStartupTimingTrace.Start();
            firstLoginStartupTiming = null;
            var startupSnapshot = LoginStartupSnapshotLoader.Load();
            startupTiming.MarkSnapshotLoaded();
            ShellThemeCoordinator.ApplyTheme(this, startupSnapshot.Theme);
            startupTiming.MarkThemeApplied();
            var loginWindow = new LoginWindow(startupSnapshot);
            startupTiming.MarkLoginWindowConstructed();
            Task<PreparedLoginEntryRuntime?>? preparedLoginEntryRuntimeTask = null;
            Task<PreparedLoginEntryRuntime?> EnsureLoginEntryRuntimeAsync()
            {
                return preparedLoginEntryRuntimeTask ??= LoginRuntimePreparer.InitializeLoginEntryRuntimeAsync(loginWindow);
            }

            loginWindow.AttachDeferredWorkflow(new DeferredLoginRuntimeWorkflow(EnsureLoginEntryRuntimeAsync));
            startupTiming.MarkLoginReady();
            loginWindow.ContentRendered += (_, _) =>
            {
                startupTiming.MarkLoginContentRendered();
                _ = startupTiming.FlushAfterFirstPaintAsync();
                _ = EnsureLoginEntryRuntimeAsync();
            };
            MainWindow = loginWindow;
            if (loginWindow.ShowDialog() != true)
            {
                LoginRuntimePreparer.DisposeWhenReadyWithoutBlockingShutdown(preparedLoginEntryRuntimeTask);
                ShutdownHostProcess();
                return;
            }

            var postLoginTiming = loginWindow.PostLoginTiming;
            var preparedLoginEntryRuntime = await EnsureLoginEntryRuntimeAsync();
            if (preparedLoginEntryRuntime is null)
            {
                ShutdownHostProcess();
                return;
            }

            var preparedLoginRuntime = await preparedLoginEntryRuntime.ShellRuntimeTask;
            postLoginTiming?.MarkRuntimeReady();
            if (preparedLoginRuntime is null)
            {
                ShutdownHostProcess();
                return;
            }

            var authenticatedSession = loginWindow.AuthenticatedSession;
            if (authenticatedSession is not null)
            {
                var sessionActivation = preparedLoginRuntime.LoginRuntime.SessionLifecycleService.ActivateExisting(authenticatedSession);
                if (!sessionActivation.IsSuccess)
                {
                    await preparedLoginRuntime.DisposeAsync();
                    ShutdownHostProcess();
                    return;
                }
            }

            var returnToLogin = await PostLoginShellPresenter.ShowAsync(
                this,
                preparedLoginRuntime,
                authenticatedSession,
                startupSnapshot.Theme,
                postLoginTiming);
            if (!returnToLogin)
            {
                ShutdownHostProcess();
                return;
            }
        }
    }

    private void ShutdownHostProcess()
    {
        ProcessExitEscalation.Begin();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        singleInstanceActivationListener?.Dispose();
        singleInstanceActivationListener = null;
        singleInstanceCoordinator?.Dispose();
        base.OnExit(e);
    }
}

using Dobby.Contracts.BackgroundOperations;
using Dobby.Core;
using Dobby.Localization;
using Dobby.PluginFramework.Packaging;
using Dobby.Shell;

namespace Dobby.Host.Plugins;

public sealed record ShellPluginManagerRuntimeActions(
    Func<string, CorrelationId, CancellationToken, IProgress<PluginPackageInstallProgress>, Task<Result<InstalledPluginState>>> InstallPackageAsync,
    Func<string, CorrelationId, CancellationToken, Task<Result>> EnablePluginAsync,
    Func<string, CorrelationId, CancellationToken, Task<Result>> DisablePluginAsync,
    Func<string, PluginUninstallDataChoice, CorrelationId, CancellationToken, Task<Result>> UninstallPluginAsync);

public sealed class ShellPluginManagerOperationCoordinator : IAsyncDisposable
{
    private readonly ShellViewModel shellViewModel;
    private readonly IBackgroundOperationReporter? backgroundOperations;
    private readonly ShellPluginManagerRuntimeActions actions;
    private readonly Action refreshSurface;
    private readonly CancellationToken authenticatedLifetime;
    private readonly CancellationTokenSource shutdown = new();
    private readonly object activeGate = new();
    private Task? activeOperation;
    private CancellationTokenSource? activeCancellation;
    private int activeCanCancel;
    private int operationInProgress;

    public ShellPluginManagerOperationCoordinator(
        ShellViewModel shellViewModel,
        IBackgroundOperationReporter? backgroundOperations,
        ShellPluginManagerRuntimeActions actions,
        Action refreshSurface,
        CancellationToken authenticatedLifetime = default)
    {
        this.shellViewModel = shellViewModel ?? throw new ArgumentNullException(nameof(shellViewModel));
        this.backgroundOperations = backgroundOperations;
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        this.refreshSurface = refreshSurface ?? throw new ArgumentNullException(nameof(refreshSurface));
        this.authenticatedLifetime = authenticatedLifetime;
    }

    public Task<Result<InstalledPluginState>> InstallAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        if (!TryBeginOperation())
        {
            return Task.FromResult(Result.Failure<InstalledPluginState>(ErrorCode.InvalidInput));
        }

        var operation = RunInstallAsync(packagePath, cancellationToken);
        Track(operation);
        return operation;
    }

    public Task<Result> EnableAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        return RunLifecycleAsync(
            pluginId,
            HostResourceKeys.PluginsManagerEnableBusy,
            actions.EnablePluginAsync,
            cancellationToken);
    }

    public Task<Result> DisableAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        return RunLifecycleAsync(
            pluginId,
            HostResourceKeys.PluginsManagerDisableBusy,
            actions.DisablePluginAsync,
            cancellationToken);
    }

    public Task<Result> UninstallAsync(
        string pluginId,
        PluginUninstallDataChoice dataChoice,
        CancellationToken cancellationToken = default)
    {
        return RunLifecycleAsync(
            pluginId,
            HostResourceKeys.PluginsManagerUninstallBusy,
            (targetPluginId, correlationId, token) =>
                actions.UninstallPluginAsync(targetPluginId, dataChoice, correlationId, token),
            cancellationToken);
    }

    public bool CancelActiveOperation()
    {
        lock (activeGate)
        {
            if (activeCancellation is null || Volatile.Read(ref activeCanCancel) == 0)
            {
                return false;
            }

            activeCancellation.Cancel();
            return true;
        }
    }

    public async Task CancelAndDrainAsync()
    {
        shutdown.Cancel();
        Task? operation;
        lock (activeGate)
        {
            activeCancellation?.Cancel();
            operation = activeOperation;
        }

        if (operation is null)
        {
            return;
        }

        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CancelAndDrainAsync().ConfigureAwait(false);
        shutdown.Dispose();
    }

    private async Task<Result<InstalledPluginState>> RunInstallAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId.New();
        var operationId = Guid.NewGuid();
        using var linked = CreateOperationCancellation(cancellationToken);
        SetActiveCancellation(linked, canCancel: true);
        shellViewModel.SetPluginsManagerBusy(true, HostResourceKeys.PluginsManagerInstallBusy);
        Report(
            operationId,
            correlationId,
            HostResourceKeys.PluginsManagerInstallBusy,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Determinate(0),
            canCancel: true,
            failureReason: null);

        var progress = new ImmediateProgress<PluginPackageInstallProgress>(value =>
        {
            SetActiveCanCancel(value.CanCancel);
            Report(
                operationId,
                correlationId,
                HostResourceKeys.PluginsManagerInstallBusy,
                BackgroundOperationStatus.Running,
                BackgroundOperationProgress.Determinate(value.Percent),
                value.CanCancel,
                failureReason: null);
        });

        Result<InstalledPluginState> result;
        try
        {
            result = await actions.InstallPackageAsync(
                packagePath,
                correlationId,
                linked.Token,
                progress);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            result = Result.Cancelled<InstalledPluginState>();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            result = Result.Failure<InstalledPluginState>(ErrorCode.PluginInternalError);
        }
        finally
        {
            shellViewModel.SetPluginsManagerBusy(false, HostResourceKeys.PluginsManagerInstallBusy);
        }

        Complete(result.IsSuccess ? Result.Success() : Result.Failure(result.ErrorCode ?? ErrorCode.PluginInternalError), operationId, correlationId, HostResourceKeys.PluginsManagerInstallBusy);
        return result;
    }

    private Task<Result> RunLifecycleAsync(
        string pluginId,
        string displayNameKey,
        Func<string, CorrelationId, CancellationToken, Task<Result>> action,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(action);
        if (!TryBeginOperation())
        {
            return Task.FromResult(Result.Failure(ErrorCode.InvalidInput));
        }

        var operation = RunLifecycleCoreAsync(pluginId, displayNameKey, action, cancellationToken);
        Track(operation);
        return operation;
    }

    private async Task<Result> RunLifecycleCoreAsync(
        string pluginId,
        string displayNameKey,
        Func<string, CorrelationId, CancellationToken, Task<Result>> action,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId.New();
        var operationId = Guid.NewGuid();
        using var linked = CreateOperationCancellation(cancellationToken);
        SetActiveCancellation(linked, canCancel: true);
        shellViewModel.SetPluginsManagerBusy(true, displayNameKey);
        Report(
            operationId,
            correlationId,
            displayNameKey,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Indeterminate(),
            canCancel: true,
            failureReason: null);

        Result result;
        try
        {
            result = await action(pluginId, correlationId, linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            result = Result.Failure(ErrorCode.Cancelled);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            result = Result.Failure(ErrorCode.PluginInternalError);
        }
        finally
        {
            shellViewModel.SetPluginsManagerBusy(false, displayNameKey);
        }

        Complete(result, operationId, correlationId, displayNameKey);
        return result;
    }

    private void Complete(
        Result result,
        Guid operationId,
        CorrelationId correlationId,
        string displayNameKey)
    {
        if (result.IsSuccess)
        {
            shellViewModel.SetPluginsManagerFeedback(null);
            Report(
                operationId,
                correlationId,
                displayNameKey,
                BackgroundOperationStatus.Completed,
                BackgroundOperationProgress.Determinate(100),
                canCancel: false,
                failureReason: null);
            refreshSurface();
            return;
        }

        shellViewModel.SetPluginsManagerFeedback(FeedbackKey(result.ErrorCode));
        Report(
            operationId,
            correlationId,
            displayNameKey,
            result.ErrorCode == ErrorCode.Cancelled
                ? BackgroundOperationStatus.Cancelled
                : BackgroundOperationStatus.Failed,
            BackgroundOperationProgress.Indeterminate(),
            canCancel: false,
            result.ErrorCode);
    }

    private bool TryBeginOperation()
    {
        return !shutdown.IsCancellationRequested &&
            Interlocked.CompareExchange(ref operationInProgress, 1, 0) == 0;
    }

    private void Track(Task operation)
    {
        lock (activeGate)
        {
            activeOperation = operation;
        }

        _ = operation.ContinueWith(
            completed =>
            {
                lock (activeGate)
                {
                    if (ReferenceEquals(activeOperation, completed))
                    {
                        activeOperation = null;
                        activeCancellation = null;
                        Volatile.Write(ref activeCanCancel, 0);
                    }
                }

                Interlocked.Exchange(ref operationInProgress, 0);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private CancellationTokenSource CreateOperationCancellation(CancellationToken cancellationToken)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(
            authenticatedLifetime,
            shutdown.Token,
            cancellationToken);
    }

    private void SetActiveCancellation(CancellationTokenSource cancellation, bool canCancel)
    {
        lock (activeGate)
        {
            activeCancellation = cancellation;
            Volatile.Write(ref activeCanCancel, canCancel ? 1 : 0);
        }
    }

    private void SetActiveCanCancel(bool canCancel)
    {
        Volatile.Write(ref activeCanCancel, canCancel ? 1 : 0);
    }

    private void Report(
        Guid operationId,
        CorrelationId correlationId,
        string displayNameKey,
        BackgroundOperationStatus status,
        BackgroundOperationProgress progress,
        bool canCancel,
        ErrorCode? failureReason)
    {
        backgroundOperations?.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Host,
            OwnerId: null,
            displayNameKey,
            ShortDetailKey: null,
            status,
            progress,
            canCancel,
            correlationId.Value,
            failureReason));
    }

    private static string? FeedbackKey(ErrorCode? errorCode)
    {
        if (errorCode == ErrorCode.Cancelled)
        {
            return null;
        }

        return errorCode is { } code &&
            (code == ErrorCode.Unauthorized ||
             code == ErrorCode.Forbidden ||
             code == ErrorCode.LicenseDenied ||
             code == ErrorCode.LicenseReadOnly)
            ? HostResourceKeys.PluginsManagerOperationDenied
            : HostResourceKeys.ShellFeedbackError;
    }

    private sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.PluginFramework.Packaging;
using Dobby.PluginFramework.Activation;
using Dobby.Security.Authorization;
using Dobby.Contracts.Licensing;

namespace Dobby.Host.Plugins;

public enum PluginUninstallDataChoice
{
    KeepData,
    DeleteData
}

[PluginRuntimeActionOwner(PluginRuntimeAction.EnableDisableUninstall)]
public sealed class HostPluginLifecycleCoordinator(
    IInstalledPluginStateStore stateStore,
    IInstalledPluginMetadataCache metadataCache,
    IPluginInstallStagingArea stagingArea,
    IPluginDataRetentionStore dataRetentionStore,
    IPluginCurrentProcessActivationTracker activationTracker,
    OperationAuthorizationPolicy authorizationPolicy,
    PluginRuntimeAuditCoordinator auditCoordinator,
    HostRuntimeModeMutationGate mutationGate,
    IPluginRuntimeLifecycleControl? runtimeLifecycleControl = null,
    HostPluginActivationCancellation? activationCancellation = null)
{
    private readonly IPluginDataRetentionStore dataRetentionStore =
        dataRetentionStore ?? throw new ArgumentNullException(nameof(dataRetentionStore));
    private readonly HostRuntimeModeMutationGate runtimeModeMutationGate = mutationGate ??
        throw new ArgumentNullException(nameof(mutationGate));
    private readonly IPluginRuntimeLifecycleControl? lifecycleControl = runtimeLifecycleControl;
    private readonly HostPluginActivationCancellation? activationCancellationSource = activationCancellation;

    public Result<IReadOnlyList<InstalledPluginMetadata>> ListPlugins(string actorId, CorrelationId correlationId)
    {
        var authorized = Authorize(actorId, OperationVerb.View, correlationId);
        return authorized.IsSuccess
            ? metadataCache.Snapshot()
            : Result.Failure<IReadOnlyList<InstalledPluginMetadata>>(authorized.ErrorCode ?? ErrorCode.Forbidden);
    }

    public Result EnablePlugin(string actorId, string pluginId, CorrelationId correlationId)
    {
        return SetEnabled(
            actorId,
            pluginId,
            isEnabled: true,
            "Enabled",
            OperationVerb.Configure,
            correlationId,
            CancellationToken.None);
    }

    public Result DisablePlugin(string actorId, string pluginId, CorrelationId correlationId)
    {
        return SetEnabled(
            actorId,
            pluginId,
            isEnabled: false,
            "Disabled",
            OperationVerb.Configure,
            correlationId,
            CancellationToken.None);
    }

    public Task<Result> EnablePluginAsync(
        string actorId,
        string pluginId,
        CorrelationId correlationId,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(
            token => SetEnabled(
                actorId,
                pluginId,
                isEnabled: true,
                "Enabled",
                OperationVerb.Configure,
                correlationId,
                token),
            cancellationToken);
    }

    public Task<Result> DisablePluginAsync(
        string actorId,
        string pluginId,
        CorrelationId correlationId,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(
            token => SetEnabled(
                actorId,
                pluginId,
                isEnabled: false,
                "Disabled",
                OperationVerb.Configure,
                correlationId,
                token),
            cancellationToken);
    }

    public Result UninstallPlugin(
        string actorId,
        string pluginId,
        PluginUninstallDataChoice dataChoice,
        CorrelationId correlationId)
    {
        return UninstallPluginCore(
            actorId,
            pluginId,
            dataChoice,
            correlationId,
            CancellationToken.None);
    }

    public Task<Result> UninstallPluginAsync(
        string actorId,
        string pluginId,
        PluginUninstallDataChoice dataChoice,
        CorrelationId correlationId,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(
            token => UninstallPluginCore(actorId, pluginId, dataChoice, correlationId, token),
            cancellationToken);
    }

    private Result UninstallPluginCore(
        string actorId,
        string pluginId,
        PluginUninstallDataChoice dataChoice,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        cancellationToken.ThrowIfCancellationRequested();

        var authorized = Authorize(actorId, OperationVerb.Delete, correlationId);
        if (!authorized.IsSuccess)
        {
            return authorized;
        }

        var mutationAllowed = runtimeModeMutationGate.EnsurePluginLifecycleMutationAllowed(pluginId);
        if (!mutationAllowed.IsSuccess)
        {
            return mutationAllowed;
        }

        var existing = stateStore.Get(pluginId);
        if (!existing.IsSuccess)
        {
            return Result.Failure(existing.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        if (existing.Value is null)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        if (existing.Value.IsPendingUninstall)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }

        var choice = ToWireValue(dataChoice);
        var wasActivatedInCurrentProcess = WasActivatedInCurrentProcess(pluginId);
        if (wasActivatedInCurrentProcess)
        {
            var deactivate = DeactivateRuntime(pluginId, cancellationToken);
            if (!deactivate.IsSuccess)
            {
                return deactivate;
            }

            var now = DateTimeOffset.UtcNow;
            var pending = existing.Value with
            {
                IsEnabled = false,
                IsPendingUninstall = true,
                DataRetentionChoice = choice,
                RestartRequired = true,
                StatusSummary = "Pending uninstall",
                UpdatedAtUtc = now
            };
            var updateState = stateStore.Upsert(pending);
            if (!updateState.IsSuccess)
            {
                return Result.Failure(updateState.ErrorCode ?? ErrorCode.DatabaseUnavailable);
            }

            var updateCache = metadataCache.Upsert(ToMetadata(pending));
            if (!updateCache.IsSuccess)
            {
                return Result.Failure(updateCache.ErrorCode ?? ErrorCode.DatabaseUnavailable);
            }

            auditCoordinator.AppendPluginUninstalled(actorId, pluginId, choice, restartRequired: true, correlationId);
            return Result.Success();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var cleanup = stagingArea.RemoveRuntime(existing.Value.RuntimePath) ?? stagingArea.Cleanup(existing.Value.RuntimePath);
        if (!cleanup.IsSuccess)
        {
            return Result.Failure(cleanup.ErrorCode ?? ErrorCode.IoFailed);
        }

        if (dataChoice == PluginUninstallDataChoice.DeleteData)
        {
            var deleteData = dataRetentionStore.DeletePluginData(pluginId);
            if (!deleteData.IsSuccess)
            {
                return Result.Failure(deleteData.ErrorCode ?? ErrorCode.IoFailed);
            }
        }

        var removeState = stateStore.Remove(pluginId);
        if (!removeState.IsSuccess)
        {
            return Result.Failure(removeState.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        var removeCache = metadataCache.Remove(pluginId);
        if (!removeCache.IsSuccess)
        {
            return Result.Failure(removeCache.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        auditCoordinator.AppendPluginUninstalled(actorId, pluginId, choice, restartRequired: false, correlationId);
        return Result.Success();
    }

    public bool WasActivatedInCurrentProcess(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return activationTracker.IsActivatedInCurrentProcess(pluginId);
    }

    private Result SetEnabled(
        string actorId,
        string pluginId,
        bool isEnabled,
        string statusSummary,
        OperationVerb verb,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        cancellationToken.ThrowIfCancellationRequested();

        var authorized = Authorize(actorId, verb, correlationId);
        if (!authorized.IsSuccess)
        {
            return authorized;
        }

        var mutationAllowed = runtimeModeMutationGate.EnsurePluginLifecycleMutationAllowed(pluginId);
        if (!mutationAllowed.IsSuccess)
        {
            return mutationAllowed;
        }

        var existing = stateStore.Get(pluginId);
        if (!existing.IsSuccess)
        {
            return Result.Failure(existing.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        if (existing.Value is null)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        if (existing.Value.IsPendingUninstall)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }

        if (!isEnabled && WasActivatedInCurrentProcess(pluginId))
        {
            var deactivate = DeactivateRuntime(pluginId, cancellationToken);
            if (!deactivate.IsSuccess)
            {
                return deactivate;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var updateState = stateStore.SetEnabled(pluginId, isEnabled, statusSummary);
        if (!updateState.IsSuccess)
        {
            return Result.Failure(updateState.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        var updated = existing.Value with
        {
            IsEnabled = isEnabled,
            StatusSummary = statusSummary,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var updateCache = metadataCache.Upsert(ToMetadata(updated));
        if (!updateCache.IsSuccess)
        {
            return Result.Failure(updateCache.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        if (isEnabled)
        {
            auditCoordinator.AppendPluginEnabled(actorId, pluginId, correlationId);
        }
        else
        {
            auditCoordinator.AppendPluginDisabled(actorId, pluginId, correlationId);
        }

        return Result.Success();
    }

    private Result DeactivateRuntime(string pluginId, CancellationToken cancellationToken)
    {
        activationCancellationSource?.CancelPostLoginActivation();
        if (lifecycleControl is null)
        {
            return Result.Success();
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var hostOperation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        try
        {
            return lifecycleControl.DeactivateAndUnloadAsync(pluginId, hostOperation.Token)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException)
        {
            return Result.Failure(ErrorCode.Cancelled);
        }
    }

    private static Task<Result> RunAsync(
        Func<CancellationToken, Result> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Task.Factory.StartNew(
            () =>
            {
                try
                {
                    return action(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure(ErrorCode.Cancelled);
                }
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    private Result Authorize(string actorId, OperationVerb verb, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        var operationKey = new OperationKey(BuiltInOperationTargets.PluginManagementTargetId, verb).ToString();
        var decision = authorizationPolicy.Evaluate(
            new OperationPermissionCapability(operationKey),
            new PolicyContext(actorId, null, correlationId));
        if (!decision.IsSuccess)
        {
            return Result.Failure(decision.ErrorCode ?? ErrorCode.Forbidden);
        }

        return decision.Value.Allowed
            ? Result.Success()
            : Result.Failure(ErrorCode.Forbidden);
    }

    private static InstalledPluginMetadata ToMetadata(InstalledPluginState state)
    {
        return new InstalledPluginMetadata(
            state.PluginId,
            state.DisplayName,
            state.Version,
            state.Description,
            state.PackageFingerprint,
            state.IsEnabled,
            state.StatusSummary,
            state.InstalledAtUtc,
            state.UpdatedAtUtc,
            state.IsPendingUninstall,
            state.RestartRequired);
    }

    private static string ToWireValue(PluginUninstallDataChoice dataChoice)
    {
        return dataChoice switch
        {
            PluginUninstallDataChoice.KeepData => "keep",
            PluginUninstallDataChoice.DeleteData => "delete",
            _ => throw new ArgumentOutOfRangeException(nameof(dataChoice), dataChoice, "Unsupported plugin uninstall data choice.")
        };
    }
}

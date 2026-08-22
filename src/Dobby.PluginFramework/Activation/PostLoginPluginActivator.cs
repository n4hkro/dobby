using Dobby.Contracts.PluginFramework;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Core.Versioning;

namespace Dobby.PluginFramework.Activation;

public sealed record PluginActivationCandidate(
    string PluginId,
    string Version,
    bool IsEnabled,
    IReadOnlyList<PluginDependency> Dependencies,
    Func<CancellationToken, Result<IPluginModule>> ModuleFactory);

public sealed record PluginActivationRun(
    IReadOnlyList<PluginActivationOutcome> Outcomes,
    IReadOnlyList<string> ActivatedPluginIds,
    IReadOnlyList<PluginNavigationContribution> NavigationContributions);

public enum PluginActivationCompletion
{
    Completed,
    Cancelled
}

public sealed record PluginActivationExecutionOutcome(
    PluginActivationCompletion Completion,
    PluginActivationRun Run);

public sealed class PostLoginPluginActivator(
    PluginServiceRegistrationBridge serviceBridge,
    PluginLifecycleExecutionOptions? lifecycleOptions = null) : IAsyncDisposable, IPluginRuntimeLifecycleControl
{
    public const string LegacyV1UpgradeWarning = "legacy-v1-adapter-upgrade-required";
    private readonly object activeGate = new();
    private readonly Dictionary<string, ActiveModule> activeModules = new(StringComparer.Ordinal);
    private readonly PluginLifecycleExecutionOptions options = lifecycleOptions ?? PluginLifecycleExecutionOptions.Default;

    public Result<PluginActivationRun> Activate(
        IReadOnlyList<PluginActivationCandidate> candidates,
        HostVersion hostVersion,
        CancellationToken cancellationToken)
    {
        var result = ActivateAsync(candidates, hostVersion, cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        if (!result.IsSuccess)
        {
            return Result.Failure<PluginActivationRun>(result.ErrorCode ?? ErrorCode.PluginInternalError);
        }

        return result.Value.Completion == PluginActivationCompletion.Completed
            ? Result.Success(result.Value.Run)
            : Result.Failure<PluginActivationRun>(ErrorCode.Cancelled);
    }

    public async ValueTask<Result<PluginActivationExecutionOutcome>> ActivateAsync(
        IReadOnlyList<PluginActivationCandidate> candidates,
        HostVersion hostVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(serviceBridge);
        options.Validate();

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Success(new PluginActivationExecutionOutcome(
                PluginActivationCompletion.Cancelled,
                BuildRun(new Dictionary<string, PluginActivationOutcome>(StringComparer.Ordinal), [])));
        }

        var byId = candidates.ToDictionary(candidate => candidate.PluginId, StringComparer.Ordinal);
        var resolution = PluginDependencyGraph.Resolve(
            candidates.Select(candidate => new PluginDependencyCandidate(
                candidate.PluginId,
                candidate.Version,
                candidate.IsEnabled,
                candidate.Dependencies)).ToArray());

        var outcomes = resolution.Outcomes.ToDictionary(outcome => outcome.PluginId, StringComparer.Ordinal);
        var activated = new List<string>();
        var failedPlugins = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in resolution.ActivationOrder)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Result.Success(new PluginActivationExecutionOutcome(
                    PluginActivationCompletion.Cancelled,
                    BuildRun(outcomes, activated)));
            }

            var failedDependency = candidate.Dependencies
                .Select(dependency => dependency.PluginId)
                .FirstOrDefault(failedPlugins.Contains);
            if (failedDependency is not null)
            {
                outcomes[candidate.PluginId] = new PluginActivationOutcome(
                    candidate.PluginId,
                    PluginActivationStatus.Skipped,
                    $"dependency-activation-failed: {failedDependency}");
                continue;
            }

            if (!byId.TryGetValue(candidate.PluginId, out var current))
            {
                outcomes[candidate.PluginId] = new PluginActivationOutcome(
                    candidate.PluginId,
                    PluginActivationStatus.Faulted,
                    ErrorCode.NotFound.ToString());
                failedPlugins.Add(candidate.PluginId);
                continue;
            }

            Result<IPluginModule> moduleResult;
            PluginInvocationOutcomeKind? moduleFactoryExceptionKind = null;
            try
            {
                moduleResult = current.ModuleFactory(cancellationToken);
                if (moduleResult.IsSuccess && moduleResult.Value is null)
                {
                    moduleResult = Result.Failure<IPluginModule>(ErrorCode.SerializationFailed);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                moduleResult = Result.Failure<IPluginModule>(ErrorCode.Cancelled);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                moduleResult = Result.Failure<IPluginModule>(ErrorCode.PluginInternalError);
                moduleFactoryExceptionKind = PluginInvocationOutcomeKind.PluginException;
            }
            if (!moduleResult.IsSuccess)
            {
                if (moduleResult.ErrorCode == ErrorCode.Cancelled)
                {
                    return Result.Success(new PluginActivationExecutionOutcome(
                        PluginActivationCompletion.Cancelled,
                        BuildRun(outcomes, activated)));
                }
                var moduleKind = moduleFactoryExceptionKind ?? PluginExceptionBoundary.ClassifyResult(
                    isSuccess: false,
                    moduleResult.ErrorCode);
                outcomes[current.PluginId] = new PluginActivationOutcome(
                    current.PluginId,
                    ActivationFailureStatus(moduleKind),
                    moduleResult.ErrorCode?.ToString() ?? ErrorCode.PluginInternalError.ToString(),
                    InvocationKind: moduleKind);
                failedPlugins.Add(current.PluginId);
                continue;
            }

            var prepareResult = serviceBridge.PrepareRegistration(current.PluginId, moduleResult.Value);
            if (!prepareResult.IsSuccess)
            {
                if (prepareResult.ErrorCode == ErrorCode.Cancelled)
                {
                    return Result.Success(new PluginActivationExecutionOutcome(
                        PluginActivationCompletion.Cancelled,
                        BuildRun(outcomes, activated)));
                }

                var prepareKind = prepareResult.ErrorCode == ErrorCode.PluginInternalError
                    ? PluginInvocationOutcomeKind.PluginException
                    : PluginExceptionBoundary.ClassifyResult(isSuccess: false, prepareResult.ErrorCode);
                outcomes[current.PluginId] = new PluginActivationOutcome(
                    current.PluginId,
                    ActivationFailureStatus(prepareKind),
                    prepareResult.ErrorCode?.ToString() ?? ErrorCode.PluginInternalError.ToString(),
                    InvocationKind: prepareKind);
                failedPlugins.Add(current.PluginId);
                continue;
            }

            var hostServices = serviceBridge.HostServices;
            var activationContext = new PluginActivationContext(
                current.PluginId,
                current.Version,
                CorrelationId.New(),
                hostServices.ResolveEventBus(current.PluginId),
                hostServices.BackgroundOperations,
                hostServices.BackgroundOperationCancellation);
            var module = moduleResult.Value;
            string? warning = null;
            Result activate;
            PluginInvocationOutcomeKind activationKind;
            if (module is IPluginModuleV2 moduleV2)
            {
                var initializeOutcome = await PluginExceptionBoundary.InvokeLifecycleClassifiedAsync(
                    token => moduleV2.InitializeAsync(activationContext, token),
                    cancellationToken,
                    options.InitializationTimeout).ConfigureAwait(false);
                var initialize = initializeOutcome.Result;
                if (!initialize.IsSuccess)
                {
                    await CleanupUncommittedModuleAsync(current.PluginId, moduleV2, cancellationToken).ConfigureAwait(false);
                    if (initialize.ErrorCode == ErrorCode.Cancelled)
                    {
                        return Result.Success(new PluginActivationExecutionOutcome(
                            PluginActivationCompletion.Cancelled,
                            BuildRun(outcomes, activated)));
                    }

                    outcomes[current.PluginId] = new PluginActivationOutcome(
                        current.PluginId,
                        ActivationFailureStatus(initializeOutcome.Kind),
                        LifecycleFailureReason("initialize", initialize.ErrorCode),
                        InvocationKind: initializeOutcome.Kind);
                    failedPlugins.Add(current.PluginId);
                    continue;
                }

                var activateOutcome = await PluginExceptionBoundary.InvokeLifecycleClassifiedAsync(
                    token => moduleV2.ActivateAsync(activationContext, token),
                    cancellationToken,
                    options.ActivationTimeout).ConfigureAwait(false);
                activate = activateOutcome.Result;
                activationKind = activateOutcome.Kind;
            }
            else
            {
                warning = LegacyV1UpgradeWarning;
                var activateOutcome = await PluginExceptionBoundary.InvokeLifecycleClassifiedAsync(
                    token => new ValueTask<Result>(Task.Run(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        module.OnActivated(activationContext);
                        return Result.Success();
                    }, token)),
                    cancellationToken,
                    options.ActivationTimeout).ConfigureAwait(false);
                activate = activateOutcome.Result;
                activationKind = activateOutcome.Kind;
            }

            if (!activate.IsSuccess)
            {
                if (module is IPluginModuleV2 failedModuleV2)
                {
                    await CleanupUncommittedModuleAsync(current.PluginId, failedModuleV2, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await serviceBridge.CleanupPluginAsync(current.PluginId).ConfigureAwait(false);
                }

                if (activate.ErrorCode == ErrorCode.Cancelled)
                {
                    return Result.Success(new PluginActivationExecutionOutcome(
                        PluginActivationCompletion.Cancelled,
                        BuildRun(outcomes, activated)));
                }

                outcomes[current.PluginId] = new PluginActivationOutcome(
                    current.PluginId,
                    ActivationFailureStatus(activationKind),
                    LifecycleFailureReason("activate", activate.ErrorCode),
                    InvocationKind: activationKind);
                failedPlugins.Add(current.PluginId);
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                if (module is IPluginModuleV2 cancelledModuleV2)
                {
                    await CleanupUncommittedModuleAsync(current.PluginId, cancelledModuleV2, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await serviceBridge.CleanupPluginAsync(current.PluginId).ConfigureAwait(false);
                }
                return Result.Success(new PluginActivationExecutionOutcome(
                    PluginActivationCompletion.Cancelled,
                    BuildRun(outcomes, activated)));
            }

            var commit = await serviceBridge.CommitPreparedAsync(prepareResult.Value, cancellationToken).ConfigureAwait(false);
            if (!commit.IsSuccess)
            {
                if (module is IPluginModuleV2 commitFailedModuleV2)
                {
                    await CleanupUncommittedModuleAsync(current.PluginId, commitFailedModuleV2, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await serviceBridge.CleanupPluginAsync(current.PluginId).ConfigureAwait(false);
                }
                if (commit.ErrorCode == ErrorCode.Cancelled)
                {
                    return Result.Success(new PluginActivationExecutionOutcome(
                        PluginActivationCompletion.Cancelled,
                        BuildRun(outcomes, activated)));
                }

                return Result.Failure<PluginActivationExecutionOutcome>(
                    commit.ErrorCode ?? ErrorCode.PluginInternalError);
            }

            lock (activeGate)
            {
                activeModules[current.PluginId] = new ActiveModule(module);
            }
            activated.Add(current.PluginId);
            outcomes[current.PluginId] = new PluginActivationOutcome(
                current.PluginId,
                PluginActivationStatus.Active,
                string.Empty,
                warning,
                PluginInvocationOutcomeKind.Success);
        }

        return Result.Success(new PluginActivationExecutionOutcome(
            PluginActivationCompletion.Completed,
            BuildRun(outcomes, activated)));
    }

    public async ValueTask RollbackActivatedPluginsAsync(IReadOnlyList<string> activatedPluginIds)
    {
        ArgumentNullException.ThrowIfNull(activatedPluginIds);

        for (var index = activatedPluginIds.Count - 1; index >= 0; index--)
        {
            var pluginId = activatedPluginIds[index];
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                continue;
            }

            try
            {
                using var rollbackSource = new CancellationTokenSource(
                    options.DeactivationTimeout + options.UnloadTimeout);
                await DeactivateAndUnloadAsync(pluginId, rollbackSource.Token).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        string[] pluginIds;
        lock (activeGate)
        {
            pluginIds = activeModules.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        for (var index = pluginIds.Length - 1; index >= 0; index--)
        {
            using var shutdownSource = new CancellationTokenSource(
                options.DeactivationTimeout + options.UnloadTimeout);
            await DeactivateAndUnloadAsync(pluginIds[index], shutdownSource.Token).ConfigureAwait(false);
        }

        await serviceBridge.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask<Result> DeactivateAndUnloadAsync(
        string pluginId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        ActiveModule? active;
        lock (activeGate)
        {
            activeModules.Remove(pluginId, out active);
        }

        if (active is null)
        {
            await serviceBridge.CleanupPluginAsync(pluginId).ConfigureAwait(false);
            return Result.Success();
        }

        Result lifecycleResult = Result.Success();
        if (active.Module is IPluginModuleV2 moduleV2)
        {
            lifecycleResult = await InvokeCleanupPhaseAsync(
                token => moduleV2.DeactivateAsync(token),
                options.DeactivationTimeout).ConfigureAwait(false);
            var unload = await InvokeCleanupPhaseAsync(
                token => moduleV2.UnloadAsync(token),
                options.UnloadTimeout).ConfigureAwait(false);
            if (lifecycleResult.IsSuccess && !unload.IsSuccess)
            {
                lifecycleResult = unload;
            }
        }

        await serviceBridge.CleanupPluginAsync(pluginId).ConfigureAwait(false);
        return cancellationToken.IsCancellationRequested
            ? Result.Failure(ErrorCode.Cancelled)
            : lifecycleResult;
    }

    private async ValueTask CleanupUncommittedModuleAsync(
        string pluginId,
        IPluginModuleV2 module,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        await InvokeCleanupPhaseAsync(
            token => module.DeactivateAsync(token),
            options.DeactivationTimeout).ConfigureAwait(false);
        await InvokeCleanupPhaseAsync(
            token => module.UnloadAsync(token),
            options.UnloadTimeout).ConfigureAwait(false);
        await serviceBridge.CleanupPluginAsync(pluginId).ConfigureAwait(false);
    }

    private static string LifecycleFailureReason(string phase, ErrorCode? errorCode) =>
        $"lifecycle-{phase}-{(errorCode ?? ErrorCode.PluginInternalError).Value.ToLowerInvariant()}";

    private static PluginActivationStatus ActivationFailureStatus(
        PluginInvocationOutcomeKind outcomeKind) =>
        PluginExceptionBoundary.IsPluginDefect(outcomeKind)
            ? PluginActivationStatus.Faulted
            : PluginActivationStatus.Rejected;

    private async ValueTask<Result> InvokeCleanupPhaseAsync(
        Func<CancellationToken, ValueTask<Result>> action,
        TimeSpan timeout)
    {
        using var hostCleanupSource = new CancellationTokenSource(timeout);
        return await PluginExceptionBoundary.InvokeLifecycleAsync(
            action,
            hostCleanupSource.Token,
            timeout).ConfigureAwait(false);
    }

    private static PluginActivationRun BuildRun(
        IReadOnlyDictionary<string, PluginActivationOutcome> outcomes,
        IReadOnlyList<string> activated)
    {
        return new PluginActivationRun(
            outcomes.Values.OrderBy(outcome => outcome.PluginId, StringComparer.Ordinal).ToArray(),
            activated.ToArray(),
            []);
    }

    private sealed record ActiveModule(IPluginModule Module);
}

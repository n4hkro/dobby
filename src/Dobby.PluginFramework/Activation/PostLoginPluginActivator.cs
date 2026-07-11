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
    PluginServiceRegistrationBridge serviceBridge) : IAsyncDisposable
{
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

            var moduleResult = current.ModuleFactory(cancellationToken);
            if (!moduleResult.IsSuccess)
            {
                outcomes[current.PluginId] = new PluginActivationOutcome(
                    current.PluginId,
                    PluginActivationStatus.Faulted,
                    moduleResult.ErrorCode?.ToString() ?? ErrorCode.PluginInternalError.ToString());
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

                outcomes[current.PluginId] = new PluginActivationOutcome(
                    current.PluginId,
                    PluginActivationStatus.Faulted,
                    prepareResult.ErrorCode?.ToString() ?? ErrorCode.PluginInternalError.ToString());
                failedPlugins.Add(current.PluginId);
                continue;
            }

            var hostServices = serviceBridge.HostServices;
            var activate = await PluginExceptionBoundary.InvokeAsync(
                () =>
                {
                    moduleResult.Value.OnActivated(new PluginActivationContext(
                        current.PluginId,
                        current.Version,
                        CorrelationId.New(),
                        hostServices.ResolveEventBus(current.PluginId),
                        hostServices.BackgroundOperations,
                        hostServices.BackgroundOperationCancellation));
                    return Task.FromResult(true);
                },
                cancellationToken).ConfigureAwait(false);
            if (!activate.IsSuccess)
            {
                await serviceBridge.CleanupPluginAsync(current.PluginId).ConfigureAwait(false);
                if (activate.ErrorCode == ErrorCode.Cancelled)
                {
                    return Result.Success(new PluginActivationExecutionOutcome(
                        PluginActivationCompletion.Cancelled,
                        BuildRun(outcomes, activated)));
                }

                outcomes[current.PluginId] = new PluginActivationOutcome(
                    current.PluginId,
                    PluginActivationStatus.Faulted,
                    activate.ErrorCode?.ToString() ?? ErrorCode.PluginInternalError.ToString());
                failedPlugins.Add(current.PluginId);
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                await serviceBridge.CleanupPluginAsync(current.PluginId).ConfigureAwait(false);
                return Result.Success(new PluginActivationExecutionOutcome(
                    PluginActivationCompletion.Cancelled,
                    BuildRun(outcomes, activated)));
            }

            var commit = await serviceBridge.CommitPreparedAsync(prepareResult.Value, cancellationToken).ConfigureAwait(false);
            if (!commit.IsSuccess)
            {
                await serviceBridge.CleanupPluginAsync(current.PluginId).ConfigureAwait(false);
                if (commit.ErrorCode == ErrorCode.Cancelled)
                {
                    return Result.Success(new PluginActivationExecutionOutcome(
                        PluginActivationCompletion.Cancelled,
                        BuildRun(outcomes, activated)));
                }

                return Result.Failure<PluginActivationExecutionOutcome>(
                    commit.ErrorCode ?? ErrorCode.PluginInternalError);
            }

            activated.Add(current.PluginId);
            outcomes[current.PluginId] = new PluginActivationOutcome(current.PluginId, PluginActivationStatus.Active, string.Empty);
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
                await serviceBridge.CleanupPluginAsync(pluginId).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await serviceBridge.DisposeAsync().ConfigureAwait(false);
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
}

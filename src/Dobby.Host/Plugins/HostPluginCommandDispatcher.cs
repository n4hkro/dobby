using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.PluginPages;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.Security.Authorization;

namespace Dobby.Host.Plugins;

public sealed class HostPluginCommandDispatcher(
    PluginExtensionPointRegistry extensionPointRegistry,
    OperationAuthorizationPolicy authorizationPolicy,
    HostRuntimeModeMutationGate mutationGate,
    IBackgroundOperationReporter backgroundOperations,
    CancellationToken authenticatedLifetime,
    PluginRuntimeDiagnosticsCoordinator? diagnostics = null,
    PluginRuntimeFaultPolicy? faultPolicy = null,
    IPluginRuntimeLifecycleControl? lifecycleControl = null) : IPluginCommandDispatcher
{
    private static readonly TimeSpan MaximumCommandTimeout = TimeSpan.FromSeconds(30);

    public async ValueTask<Result<PluginCommandResult>> DispatchAsync(
        PluginCommandRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var preflight = ValidateRequest(request, timeout);
        if (!preflight.IsSuccess)
        {
            return Result.Failure<PluginCommandResult>(preflight.ErrorCode ?? ErrorCode.InvalidInput);
        }

        if (faultPolicy?.IsQuarantined(request.PluginId) == true)
        {
            return Result.Failure<PluginCommandResult>(ErrorCode.PluginInternalError);
        }

        Result runtimeAllowed;
        try
        {
            runtimeAllowed = mutationGate.EnsurePluginMutationAllowed(request.PluginId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure<PluginCommandResult>(ErrorCode.LicenseDenied);
        }
        if (!runtimeAllowed.IsSuccess)
        {
            return Result.Failure<PluginCommandResult>(runtimeAllowed.ErrorCode ?? ErrorCode.LicenseDenied);
        }

        var operationKey = OperationKey.Parse(request.OperationKey);
        if (!operationKey.IsSuccess ||
            !operationKey.Value.TargetId.StartsWith(request.PluginId + ".", StringComparison.Ordinal))
        {
            return Result.Failure<PluginCommandResult>(ErrorCode.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return Result.Failure<PluginCommandResult>(ErrorCode.Unauthorized);
        }

        var correlationId = Guid.TryParse(request.CorrelationId, out var parsedCorrelationId)
            ? new CorrelationId(parsedCorrelationId)
            : CorrelationId.New();
        Result<PolicyDecision> decision;
        try
        {
            decision = authorizationPolicy.Evaluate(
                new OperationPermissionCapability(operationKey.Value.ToString()),
                new PolicyContext(request.UserId, request.PluginId, correlationId));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure<PluginCommandResult>(ErrorCode.Forbidden);
        }
        if (!decision.IsSuccess || !decision.Value.Allowed)
        {
            return Result.Failure<PluginCommandResult>(
                decision.IsSuccess ? ErrorCode.Forbidden : decision.ErrorCode ?? ErrorCode.Forbidden);
        }

        var boundedRequest = request with
        {
            Values = new Dictionary<string, string>(request.Values, StringComparer.Ordinal)
        };
        var handler = ResolveHandler(boundedRequest.PluginId, boundedRequest.RouteKey);
        if (!handler.IsSuccess)
        {
            if (handler.ErrorCode == ErrorCode.PluginInternalError)
            {
                diagnostics?.RecordPageActionOutcome(
                    boundedRequest.PluginId,
                    boundedRequest.RouteKey,
                    boundedRequest.CommandId,
                    ErrorCode.PluginInternalError,
                    PluginInvocationOutcomeKind.PluginException,
                    boundedRequest.CorrelationId);
                await ObserveFaultPolicyAsync(
                    boundedRequest.PluginId,
                    PluginInvocationOutcomeKind.PluginException).ConfigureAwait(false);
            }
            return Result.Failure<PluginCommandResult>(handler.ErrorCode ?? ErrorCode.NotFound);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            authenticatedLifetime,
            cancellationToken);
        var operationId = Guid.CreateVersion7();
        Report(operationId, boundedRequest, BackgroundOperationStatus.Running, null);
        var effectiveTimeout = timeout < MaximumCommandTimeout ? timeout : MaximumCommandTimeout;
        var outcome = await PluginExceptionBoundary.InvokeLifecycleClassifiedAsync(
            token => handler.Value.ExecuteCommandAsync(boundedRequest, token),
            linked.Token,
            effectiveTimeout).ConfigureAwait(false);
        if (outcome.Result.IsSuccess)
        {
            var contract = outcome.Result.Value is null
                ? Result.Failure(ErrorCode.ValidationFailed)
                : PluginComponentModelValidator.ValidateCommandResult(
                    outcome.Result.Value,
                    new PluginPageCommandContext(
                        boundedRequest.PluginId,
                        boundedRequest.RouteKey,
                        boundedRequest.UserId,
                        boundedRequest.CorrelationId));
            if (!contract.IsSuccess)
            {
                outcome = new PluginInvocationOutcome<PluginCommandResult>(
                    Result.Failure<PluginCommandResult>(ErrorCode.SerializationFailed),
                    PluginInvocationOutcomeKind.ContractBreach);
            }
        }
        var result = outcome.Result;
        Report(
            operationId,
            boundedRequest,
            result.IsSuccess
                ? BackgroundOperationStatus.Completed
                : result.ErrorCode == ErrorCode.Cancelled
                    ? BackgroundOperationStatus.Cancelled
                    : BackgroundOperationStatus.Failed,
            result.ErrorCode);
        if (!result.IsSuccess)
        {
            diagnostics?.RecordPageActionOutcome(
                boundedRequest.PluginId,
                boundedRequest.RouteKey,
                boundedRequest.CommandId,
                result.ErrorCode ?? ErrorCode.PluginInternalError,
                outcome.Kind,
                boundedRequest.CorrelationId);
        }
        await ObserveFaultPolicyAsync(boundedRequest.PluginId, outcome.Kind).ConfigureAwait(false);

        return result;
    }

    private async ValueTask ObserveFaultPolicyAsync(
        string pluginId,
        PluginInvocationOutcomeKind outcomeKind)
    {
        if (faultPolicy is null)
        {
            return;
        }

        var decision = faultPolicy.Observe(pluginId, outcomeKind);
        if (!decision.QuarantineStarted || lifecycleControl is null)
        {
            return;
        }

        var cleanup = await PluginExceptionBoundary.InvokeLifecycleClassifiedAsync(
            token => lifecycleControl.DeactivateAndUnloadAsync(pluginId, token),
            CancellationToken.None,
            TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        if (!cleanup.Result.IsSuccess && cleanup.Result.ErrorCode != ErrorCode.Cancelled)
        {
            diagnostics?.RecordRuntimeFailure("plugin-quarantine-cleanup-failed");
        }
    }

    private Result<IPluginPageCommandHandler> ResolveHandler(string pluginId, string routeKey)
    {
        try
        {
            var matches = extensionPointRegistry.GetRegistrationsSnapshot<IPluginPage>()
                .Where(registration => registration.PluginId.Equals(pluginId, StringComparison.Ordinal))
                .Where(registration => PluginRouteNormalizer.Normalize(
                        registration.PluginId,
                        registration.Instance.Descriptor.RouteKey)
                    .Equals(routeKey, StringComparison.Ordinal))
                .Select(registration => registration.Instance)
                .OfType<IPluginPageCommandHandler>()
                .ToArray();
            return matches.Length == 1
                ? Result.Success<IPluginPageCommandHandler>(matches[0])
                : Result.Failure<IPluginPageCommandHandler>(
                    matches.Length == 0 ? ErrorCode.NotFound : ErrorCode.ValidationFailed);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure<IPluginPageCommandHandler>(ErrorCode.PluginInternalError);
        }
    }

    private static Result ValidateRequest(PluginCommandRequest? request, TimeSpan timeout)
    {
        try
        {
            if (request is null ||
                string.IsNullOrWhiteSpace(request.PluginId) || request.PluginId.Length > 128 ||
                string.IsNullOrWhiteSpace(request.RouteKey) || request.RouteKey.Length > 256 ||
                string.IsNullOrWhiteSpace(request.CommandId) || request.CommandId.Length > 80 ||
                string.IsNullOrWhiteSpace(request.OperationKey) || request.OperationKey.Length > 200 ||
                request.Values is null || request.Values.Count > 64 ||
                request.Values.Any(field =>
                    string.IsNullOrWhiteSpace(field.Key) || field.Key.Length > 80 ||
                    field.Value is null || field.Value.Length > 4096) ||
                request.PageRequest is { } page &&
                    (page.PageIndex < 0 || page.PageSize is < 1 or > PluginComponentModelValidator.MaximumRowsPerPage) ||
                timeout < TimeSpan.FromSeconds(1) || timeout > TimeSpan.FromSeconds(60))
            {
                return Result.Failure(ErrorCode.InvalidInput);
            }

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }
    }

    private void Report(
        Guid operationId,
        PluginCommandRequest request,
        BackgroundOperationStatus status,
        ErrorCode? failure)
    {
        try
        {
            backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
                operationId,
                BackgroundOperationOwnerKind.Plugin,
                request.PluginId,
                $"plugin.command.{request.CommandId}",
                null,
                status,
                status == BackgroundOperationStatus.Completed
                    ? BackgroundOperationProgress.Determinate(100)
                    : BackgroundOperationProgress.Indeterminate(),
                CanCancel: status == BackgroundOperationStatus.Running,
                Guid.TryParse(request.CorrelationId, out var correlationId) ? correlationId : Guid.CreateVersion7(),
                failure));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }
}

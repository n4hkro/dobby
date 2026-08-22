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
    PluginRuntimeDiagnosticsCoordinator? diagnostics = null) : IPluginCommandDispatcher
{
    private static readonly TimeSpan MaximumCommandTimeout = TimeSpan.FromSeconds(30);

    public async ValueTask<Result<PluginCommandResult>> DispatchAsync(
        PluginCommandRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preflight = ValidateRequest(request, timeout);
        if (!preflight.IsSuccess)
        {
            return Result.Failure<PluginCommandResult>(preflight.ErrorCode ?? ErrorCode.InvalidInput);
        }

        var runtimeAllowed = mutationGate.EnsurePluginMutationAllowed(request.PluginId);
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
        var decision = authorizationPolicy.Evaluate(
            new OperationPermissionCapability(operationKey.Value.ToString()),
            new PolicyContext(request.UserId, request.PluginId, correlationId));
        if (!decision.IsSuccess || !decision.Value.Allowed)
        {
            return Result.Failure<PluginCommandResult>(
                decision.IsSuccess ? ErrorCode.Forbidden : decision.ErrorCode ?? ErrorCode.Forbidden);
        }

        var handler = ResolveHandler(request.PluginId, request.RouteKey);
        if (!handler.IsSuccess)
        {
            return Result.Failure<PluginCommandResult>(handler.ErrorCode ?? ErrorCode.NotFound);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            authenticatedLifetime,
            cancellationToken);
        var operationId = Guid.CreateVersion7();
        Report(operationId, request, BackgroundOperationStatus.Running, null);
        var effectiveTimeout = timeout < MaximumCommandTimeout ? timeout : MaximumCommandTimeout;
        var result = await PluginExceptionBoundary.InvokeLifecycleAsync(
            token => handler.Value.ExecuteCommandAsync(request, token),
            linked.Token,
            effectiveTimeout).ConfigureAwait(false);
        Report(
            operationId,
            request,
            result.IsSuccess ? BackgroundOperationStatus.Completed : BackgroundOperationStatus.Failed,
            result.ErrorCode);
        if (!result.IsSuccess && result.ErrorCode != ErrorCode.Cancelled)
        {
            diagnostics?.RecordPageActionFault(
                request.PluginId,
                request.RouteKey,
                request.CommandId,
                result.ErrorCode ?? ErrorCode.PluginInternalError);
        }

        return result;
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

    private static Result ValidateRequest(PluginCommandRequest request, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(request.PluginId) || request.PluginId.Length > 128 ||
            string.IsNullOrWhiteSpace(request.RouteKey) || request.RouteKey.Length > 256 ||
            string.IsNullOrWhiteSpace(request.CommandId) || request.CommandId.Length > 80 ||
            string.IsNullOrWhiteSpace(request.OperationKey) || request.OperationKey.Length > 200 ||
            request.Values.Count > 64 ||
            request.Values.Any(field =>
                string.IsNullOrWhiteSpace(field.Key) || field.Key.Length > 80 ||
                field.Value is null || field.Value.Length > 4096) ||
            timeout < TimeSpan.FromSeconds(1) || timeout > TimeSpan.FromSeconds(60))
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }

        return Result.Success();
    }

    private void Report(
        Guid operationId,
        PluginCommandRequest request,
        BackgroundOperationStatus status,
        ErrorCode? failure)
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
}

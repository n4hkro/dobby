using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authorization;

namespace Dobby.Host.Login;

public sealed class AuthenticatedPersistenceMaintenanceCoordinator(
    IHostPersistenceMaintenanceService maintenance,
    OperationAuthorizationPolicy authorization,
    IBackgroundOperationReporter backgroundOperations,
    CancellationToken authenticatedLifetime)
{
    public Task<Result<HostPersistenceSnapshot>> CreateSnapshotAsync(
        string actorId,
        string destinationPath,
        HostPersistenceBackupPolicy policy,
        CorrelationId correlationId,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(
            actorId,
            OperationVerb.Export,
            "Dobby.Host.PersistenceBackup",
            correlationId,
            cancellationToken,
            token =>
            {
                var created = maintenance.CreateSupportSnapshot(destinationPath, policy, token);
                return created.IsSuccess
                    ? Result.Success(created.Value)
                    : Result.Failure<HostPersistenceSnapshot>(MapError(created.Error, token));
            });
    }

    public Task<Result<HostPersistenceRestoreOutcome>> RestoreSnapshotAsync(
        string actorId,
        string candidatePath,
        bool confirmCurrentStoreQuarantine,
        CorrelationId correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!confirmCurrentStoreQuarantine)
        {
            return Task.FromResult(Result.Failure<HostPersistenceRestoreOutcome>(ErrorCode.InvalidInput));
        }

        return RunAsync(
            actorId,
            OperationVerb.Import,
            "Dobby.Host.PersistenceRestore",
            correlationId,
            cancellationToken,
            token =>
            {
                var restored = maintenance.RestoreSupportSnapshot(candidatePath, token);
                return restored.IsSuccess
                    ? Result.Success(restored.Value)
                    : Result.Failure<HostPersistenceRestoreOutcome>(MapError(restored.Error, token));
            });
    }

    private async Task<Result<T>> RunAsync<T>(
        string actorId,
        OperationVerb verb,
        string operationName,
        CorrelationId correlationId,
        CancellationToken cancellationToken,
        Func<CancellationToken, Result<T>> action)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Result.Failure<T>(ErrorCode.Unauthorized);
        }

        var decision = authorization.Evaluate(
            new OperationPermissionCapability(
                new OperationKey(BuiltInOperationTargets.PersistenceMaintenanceTargetId, verb).ToString()),
            new PolicyContext(actorId, PluginId: null, correlationId));
        if (!decision.IsSuccess || !decision.Value.Allowed)
        {
            return Result.Failure<T>(ErrorCode.Forbidden);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            authenticatedLifetime,
            cancellationToken);
        var operationId = Guid.CreateVersion7();
        Report(operationId, operationName, correlationId.Value, BackgroundOperationStatus.Running, null);
        try
        {
            var result = await Task.Run(() => action(linked.Token), linked.Token).ConfigureAwait(false);
            Report(
                operationId,
                operationName,
                correlationId.Value,
                result.IsSuccess
                    ? BackgroundOperationStatus.Completed
                    : result.ErrorCode == ErrorCode.Cancelled
                        ? BackgroundOperationStatus.Cancelled
                        : BackgroundOperationStatus.Failed,
                result.ErrorCode);
            return result;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            Report(
                operationId,
                operationName,
                correlationId.Value,
                BackgroundOperationStatus.Cancelled,
                ErrorCode.Cancelled);
            return Result.Failure<T>(ErrorCode.Cancelled);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Report(
                operationId,
                operationName,
                correlationId.Value,
                BackgroundOperationStatus.Failed,
                ErrorCode.DatabaseUnavailable);
            return Result.Failure<T>(ErrorCode.DatabaseUnavailable);
        }
    }

    private void Report(
        Guid operationId,
        string operationName,
        Guid correlationId,
        BackgroundOperationStatus status,
        ErrorCode? errorCode)
    {
        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Host,
            OwnerId: null,
            operationName,
            ShortDetailKey: null,
            status,
            status == BackgroundOperationStatus.Completed
                ? BackgroundOperationProgress.Determinate(100)
                : BackgroundOperationProgress.Indeterminate(),
            CanCancel: status == BackgroundOperationStatus.Running,
            correlationId,
            errorCode));
    }

    private static ErrorCode MapError(PersistenceError error, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ErrorCode.Cancelled;
        }
        return error.Code switch
        {
            PersistenceError.ConstraintViolation => ErrorCode.InvalidInput,
            PersistenceError.TransactionConflict => ErrorCode.ResourceBusy,
            PersistenceError.SchemaMismatch => ErrorCode.ValidationFailed,
            PersistenceError.IoFailed => ErrorCode.IoFailed,
            _ => ErrorCode.DatabaseUnavailable
        };
    }
}

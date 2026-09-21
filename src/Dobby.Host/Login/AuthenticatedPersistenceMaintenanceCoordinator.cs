using Dobby.Audit;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authorization;
using System.IO;

namespace Dobby.Host.Login;

public sealed class AuthenticatedPersistenceMaintenanceCoordinator(
    IHostPersistenceMaintenanceService maintenance,
    OperationAuthorizationPolicy authorization,
    IBackgroundOperationReporter backgroundOperations,
    IAuditTrail auditTrail,
    TimeProvider timeProvider,
    CancellationToken authenticatedLifetime)
{
    private const string SnapshotTarget = "persistence.snapshot";
    private const string RestoreTarget = "persistence.restore";
    private const string UnspecifiedName = "unspecified";

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
                if (!created.IsSuccess)
                {
                    Audit(
                        AuditEventName.PersistenceSnapshotCreated,
                        actorId,
                        SnapshotTarget,
                        AuditResult.Failed,
                        correlationId,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["outputName"] = SafeName(destinationPath),
                            ["reasonCode"] = created.Error.Code
                        });
                    return Result.Failure<HostPersistenceSnapshot>(MapError(created.Error, token));
                }

                Audit(
                    AuditEventName.PersistenceSnapshotCreated,
                    actorId,
                    SnapshotTarget,
                    AuditResult.Success,
                    correlationId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["outputName"] = SafeName(created.Value.DatabasePath),
                        ["sha256"] = created.Value.Sha256
                    });
                return Result.Success(created.Value);
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
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return Task.FromResult(Result.Failure<HostPersistenceRestoreOutcome>(ErrorCode.InvalidInput));
            }

            var refusalAuditFailure = TryAudit(
                AuditEventName.PersistenceRestoreRequested,
                actorId,
                RestoreTarget,
                AuditResult.Denied,
                correlationId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["candidateName"] = SafeName(candidatePath),
                    ["reasonCode"] = "QuarantineConfirmationMissing"
                });
            return Task.FromResult(Result.Failure<HostPersistenceRestoreOutcome>(
                refusalAuditFailure ?? ErrorCode.InvalidInput));
        }

        return RunAsync(
            actorId,
            OperationVerb.Import,
            "Dobby.Host.PersistenceRestore",
            correlationId,
            cancellationToken,
            token =>
            {
                // The intent record is written into the *current* store before any destructive
                // transition, so the evidence merge in RestoreSupportSnapshot carries it into the
                // promoted store: a store that was replaced can never end up without evidence that
                // a restore ran against it. A failing intent append therefore fails closed before
                // the live store is touched (ADR-0061, docs/specs/backup-restore-migration.md
                // § 2.1-2.2).
                Audit(
                    AuditEventName.PersistenceRestoreRequested,
                    actorId,
                    RestoreTarget,
                    AuditResult.Success,
                    correlationId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["candidateName"] = SafeName(candidatePath)
                    });

                var restored = maintenance.RestoreSupportSnapshot(candidatePath, token);
                if (!restored.IsSuccess)
                {
                    var failure = MapError(restored.Error, token);
                    Audit(
                        AuditEventName.PersistenceRestoreCompleted,
                        actorId,
                        RestoreTarget,
                        AuditResult.Failed,
                        correlationId,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["candidateName"] = SafeName(candidatePath),
                            ["reasonCode"] = restored.Error.Code
                        });
                    return Result.Failure<HostPersistenceRestoreOutcome>(failure);
                }

                Audit(
                    AuditEventName.PersistenceRestoreCompleted,
                    actorId,
                    RestoreTarget,
                    AuditResult.Success,
                    correlationId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["candidateName"] = SafeName(candidatePath),
                        ["quarantineName"] = SafeName(restored.Value.QuarantinedPreviousStoreDirectory)
                    });
                return Result.Success(restored.Value);
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
            // An unauthorized maintenance attempt is security evidence in its own right, and it is
            // recorded before the refusal is returned. If even that record cannot be persisted the
            // refusal is reported as a persistence failure instead of a silent evidence loss.
            var denialAuditFailure = TryAudit(
                AuditEventName.PermissionDenied,
                actorId,
                new OperationKey(BuiltInOperationTargets.PersistenceMaintenanceTargetId, verb).ToString(),
                AuditResult.Denied,
                correlationId,
                details: null);
            return Result.Failure<T>(denialAuditFailure ?? ErrorCode.Forbidden);
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

    private void Audit(
        string eventName,
        string actorId,
        string target,
        AuditResult result,
        CorrelationId correlationId,
        IReadOnlyDictionary<string, string>? details)
    {
        auditTrail.Append(new AuditEvent(
            eventName,
            actorId,
            target,
            result,
            correlationId,
            timeProvider.GetUtcNow(),
            details));
    }

    private ErrorCode? TryAudit(
        string eventName,
        string actorId,
        string target,
        AuditResult result,
        CorrelationId correlationId,
        IReadOnlyDictionary<string, string>? details)
    {
        try
        {
            Audit(eventName, actorId, target, result, correlationId, details);
            return null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return ErrorCode.DatabaseUnavailable;
        }
    }

    /// <summary>
    /// Reduces a maintenance path to a bare artifact name so audit details stay non-disclosing:
    /// no directory chain, no drive letter and no UNC prefix ever reaches the audit trail
    /// (<c>docs/specs/audit-and-diagnostics.md</c>, <c>NFR-004</c>).
    /// </summary>
    private static string SafeName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return UnspecifiedName;
        }

        var name = Path.GetFileName(path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar));
        if (name.Length is 0 or > 128 ||
            name.Any(character =>
                char.IsControl(character) ||
                character is ':' or '\\' or '/'))
        {
            return UnspecifiedName;
        }

        return name;
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

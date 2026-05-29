using System.IO;
using System.Security.Cryptography;
using System.Text;
using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class RecoveryResponseImportCoordinator(
    IHostPersistenceTransactionScope transactionScope,
    IHostPersistenceCommandExecutor executor,
    RecoveryResponseImporter importer,
    IProtectedDataProtector credentialProtector,
    KdfPolicyResolver kdfPolicyResolver,
    IHostTransactionAuditTrail auditTrail,
    TimeProvider timeProvider,
    byte[]? applicationEntropy = null) : IRecoveryResponseImportCoordinator
{
    public Result<RecoveryVerifiedChallenge> VerifyFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return Verify(RecoveryResponseImporter.ReadEnvelope(File.ReadAllText(path)));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException)
        {
            return Result.Failure<RecoveryVerifiedChallenge>(ErrorCode.IoFailed);
        }
    }

    public Result<RecoveryVerifiedChallenge> Verify(RecoveryResponseEnvelope envelope)
    {
        var verified = importer.VerifyWithOutcome(envelope, timeProvider.GetUtcNow());
        if (!verified.Result.IsSuccess)
        {
            AppendRecoveryImportAudit(
                executor,
                LoadOwnerId(executor) ?? "owner-recovery",
                AuditResult.Failed,
                verified.Outcome,
                envelope.Payload,
                CorrelationId.New());
            return verified.Result;
        }

        if (IsNonceConsumed(verified.Result.Value.Payload.Nonce))
        {
            AppendRecoveryImportAudit(
                executor,
                LoadOwnerId(executor) ?? "owner-recovery",
                AuditResult.Failed,
                "replay",
                verified.Result.Value.Payload,
                CorrelationId.New());
            return Result.Failure<RecoveryVerifiedChallenge>(ErrorCode.Forbidden);
        }

        var ownerId = LoadOwnerId(executor);
        if (ownerId is null)
        {
            AppendRecoveryImportAudit(
                executor,
                "owner-recovery",
                AuditResult.Failed,
                "owner-missing",
                verified.Result.Value.Payload,
                CorrelationId.New());
            return Result.Failure<RecoveryVerifiedChallenge>(ErrorCode.Forbidden);
        }

        return verified.Result;
    }

    public Result ConfirmPasswordReset(RecoveryVerifiedChallenge challenge, string newPassword, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (IsExpired(challenge.Payload, timeProvider.GetUtcNow()))
        {
            AppendRecoveryImportAudit(
                executor,
                LoadOwnerId(executor) ?? "owner-recovery",
                AuditResult.Failed,
                "expired",
                challenge.Payload,
                correlationId);
            return Result.Failure(ErrorCode.Forbidden);
        }

        try
        {
            transactionScope.ExecuteInTransaction(transactionExecutor =>
            {
                if (transactionExecutor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces WHERE NonceHash = @NonceHash;", new { NonceHash = HashNonce(challenge.Payload.Nonce) }) > 0)
                {
                    throw new RecoveryImportFailedException(ErrorCode.Forbidden);
                }

                var ownerId = LoadOwnerId(transactionExecutor);
                if (ownerId is null)
                {
                    throw new RecoveryImportFailedException(ErrorCode.Forbidden);
                }

                var credentials = new CredentialStore(transactionExecutor, credentialProtector, kdfPolicyResolver, applicationEntropy);
                var rebind = credentials.RebindPassword(ownerId, newPassword, timeProvider.GetUtcNow());
                if (!rebind.IsSuccess)
                {
                    throw new RecoveryImportFailedException(rebind.ErrorCode!.Value);
                }

                transactionExecutor.Execute(
                    "INSERT INTO ConsumedRecoveryNonces(NonceHash, ConsumedAtUtc, RequestId) VALUES (@NonceHash, @ConsumedAtUtc, @RequestId);",
                    new
                    {
                        NonceHash = HashNonce(challenge.Payload.Nonce),
                        ConsumedAtUtc = timeProvider.GetUtcNow().ToString("O"),
                        challenge.Payload.RequestId
                    });

                transactionExecutor.Execute(
                    "UPDATE Sessions SET InvalidatedAtUtc = @InvalidatedAtUtc, InvalidationReason = @InvalidationReason WHERE UserId = @UserId AND InvalidatedAtUtc IS NULL;",
                    new
                    {
                        InvalidatedAtUtc = timeProvider.GetUtcNow().ToString("O"),
                        InvalidationReason = "owner-recovery",
                        UserId = ownerId
                    });

                AppendRecoveryImportAudit(
                    transactionExecutor,
                    ownerId,
                    AuditResult.Success,
                    "success",
                    challenge.Payload,
                    correlationId);

                auditTrail.AppendInScope(transactionExecutor, new AuditEvent(
                    AuditEventName.SessionInvalidated,
                    ownerId,
                    "owner-recovery",
                    AuditResult.Success,
                    correlationId,
                    timeProvider.GetUtcNow(),
                    new Dictionary<string, string> { ["reason"] = "owner-recovery" }));
            });
        }
        catch (RecoveryImportFailedException ex)
        {
            return Result.Failure(ex.ErrorCode);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }

        return Result.Success();
    }

    public Result Cancel(RecoveryVerifiedChallenge challenge, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        AppendRecoveryImportAudit(
            executor,
            LoadOwnerId(executor) ?? "owner-recovery",
            AuditResult.Failed,
            "cancelled",
            challenge.Payload,
            correlationId);
        return Result.Failure(ErrorCode.Cancelled);
    }

    public static string HashNonce(string nonce)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nonce))).ToLowerInvariant();
    }

    private void AppendRecoveryImportAudit(
        IHostPersistenceCommandExecutor auditExecutor,
        string target,
        AuditResult result,
        string outcome,
        RecoveryResponsePayload payload,
        CorrelationId correlationId)
    {
        auditTrail.AppendInScope(auditExecutor, new AuditEvent(
            AuditEventName.RecoveryImport,
            "system",
            target,
            result,
            correlationId,
            timeProvider.GetUtcNow(),
            RecoveryImportDetails(outcome, payload)));
    }

    private static Dictionary<string, string> RecoveryImportDetails(string outcome, RecoveryResponsePayload payload)
    {
        return new Dictionary<string, string>
        {
            ["requestId"] = payload.RequestId,
            ["outcome"] = outcome,
            ["nonceHash"] = HashNonce(payload.Nonce)
        };
    }

    private bool IsNonceConsumed(string nonce)
    {
        return executor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces WHERE NonceHash = @NonceHash;", new { NonceHash = HashNonce(nonce) }) > 0;
    }

    private static bool IsExpired(RecoveryResponsePayload payload, DateTimeOffset now)
    {
        return !DateTimeOffset.TryParse(payload.ExpiresAt, out var expiresAt) ||
            now.ToUniversalTime() >= expiresAt.ToUniversalTime();
    }

    private static string? LoadOwnerId(IHostPersistenceCommandExecutor executor)
    {
        var rows = executor.Query<string>("SELECT UserId FROM Users WHERE IsOwner = 1 AND IsActive = 1;");
        return rows.Count == 1 ? rows[0] : null;
    }

    private sealed class RecoveryImportFailedException(ErrorCode errorCode) : Exception
    {
        public ErrorCode ErrorCode { get; } = errorCode;
    }
}

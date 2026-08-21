using System.IO;
using System.Security.Cryptography;
using System.Text;
using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class RecoveryResponseImportCoordinator(
    IHostPersistenceCommandExecutor executor,
    RecoveryResponseImporter importer,
    CredentialStore credentialStore,
    IHostMutationUnitOfWork mutationUnitOfWork,
    IHostTransactionAuditTrail auditTrail,
    TimeProvider timeProvider,
    RecoveryAuthorityContext currentAuthority,
    string currentRecoveryPublicKeyHash) : IRecoveryResponseImportCoordinator
{
    public Result<RecoveryVerifiedChallenge> VerifyFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return Verify(RecoveryResponseImporter.ReadEnvelope(File.ReadAllText(path)));
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            System.Text.Json.JsonException)
        {
            return Result.Failure<RecoveryVerifiedChallenge>(
                exception is IOException or UnauthorizedAccessException
                    ? ErrorCode.IoFailed
                    : ErrorCode.Forbidden);
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
                envelope.Payload.RequestId,
                CorrelationId.New());
            return verified.Result;
        }

        var match = MatchStoredRequest(
            executor,
            verified.Result.Value.Payload,
            timeProvider.GetUtcNow());
        if (!match.IsSuccess)
        {
            AppendRecoveryImportAudit(
                executor,
                LoadOwnerId(executor) ?? "owner-recovery",
                AuditResult.Failed,
                match.Outcome,
                verified.Result.Value.Payload.RequestId,
                CorrelationId.New());
            return Result.Failure<RecoveryVerifiedChallenge>(ErrorCode.Forbidden);
        }

        if (LoadOwnerId(executor) is null)
        {
            AppendRecoveryImportAudit(
                executor,
                "owner-recovery",
                AuditResult.Failed,
                "owner-missing",
                verified.Result.Value.Payload.RequestId,
                CorrelationId.New());
            return Result.Failure<RecoveryVerifiedChallenge>(ErrorCode.Forbidden);
        }

        return verified.Result;
    }

    public Result ConfirmPasswordReset(
        RecoveryVerifiedChallenge challenge,
        string newPassword,
        CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (!RecoveryContractValidation.TryParseUtcZ(challenge.Payload.ExpiresAt, out var expiresAt) ||
            timeProvider.GetUtcNow().ToUniversalTime() >= expiresAt)
        {
            AppendRecoveryImportAudit(
                executor,
                LoadOwnerId(executor) ?? "owner-recovery",
                AuditResult.Failed,
                "expired",
                challenge.Payload.RequestId,
                correlationId);
            return Result.Failure(ErrorCode.Forbidden);
        }

        var ownerId = LoadOwnerId(executor);
        if (ownerId is null)
        {
            return Result.Failure(ErrorCode.Forbidden);
        }

        var preparedCredential = credentialStore.PreparePasswordRebind(
            ownerId,
            newPassword,
            timeProvider.GetUtcNow());
        if (!preparedCredential.IsSuccess)
        {
            return Result.Failure(preparedCredential.ErrorCode!.Value);
        }

        try
        {
            return mutationUnitOfWork.ExecuteResult(context =>
            {
                var match = MatchStoredRequest(
                    context.Commands,
                    challenge.Payload,
                    timeProvider.GetUtcNow());
                if (!match.IsSuccess)
                {
                    return Result.Failure(ErrorCode.Forbidden);
                }

                var transactionalOwnerId = LoadOwnerId(context.Commands);
                if (!StringComparer.Ordinal.Equals(transactionalOwnerId, ownerId))
                {
                    return Result.Failure(ErrorCode.Forbidden);
                }

                var rebound = credentialStore.ApplyPreparedCredential(
                    context.Commands,
                    preparedCredential.Value);
                if (!rebound.IsSuccess)
                {
                    return rebound;
                }

                var now = timeProvider.GetUtcNow();
                var consumed = context.Commands.Execute(
                    """
                    UPDATE RecoveryRequests
                    SET ConsumedAtUtc = @ConsumedAtUtc, ResponseId = @ResponseId
                    WHERE RequestId = @RequestId
                      AND ConsumedAtUtc IS NULL
                      AND ResponseId IS NULL;
                    """,
                    new
                    {
                        challenge.Payload.RequestId,
                        challenge.Payload.ResponseId,
                        ConsumedAtUtc = now.ToUniversalTime().ToString("O")
                    });
                if (consumed != 1)
                {
                    return Result.Failure(ErrorCode.Forbidden);
                }

                context.Commands.Execute(
                    "INSERT INTO ConsumedRecoveryNonces(NonceHash, ConsumedAtUtc, RequestId) VALUES (@NonceHash, @ConsumedAtUtc, @RequestId);",
                    new
                    {
                        NonceHash = HashNonce(challenge.Payload.Nonce),
                        ConsumedAtUtc = now.ToUniversalTime().ToString("O"),
                        challenge.Payload.RequestId
                    });
                context.Commands.Execute(
                    "UPDATE Sessions SET InvalidatedAtUtc = @InvalidatedAtUtc, InvalidationReason = 'owner-recovery' WHERE UserId = @UserId AND InvalidatedAtUtc IS NULL;",
                    new
                    {
                        InvalidatedAtUtc = now.ToUniversalTime().ToString("O"),
                        UserId = ownerId
                    });
                context.AppendAudit(CreateRecoveryImportAudit(
                    ownerId,
                    AuditResult.Success,
                    "success",
                    challenge.Payload.RequestId,
                    correlationId));
                context.AppendAudit(new AuditEvent(
                    AuditEventName.SessionInvalidated,
                    ownerId,
                    "owner-recovery",
                    AuditResult.Success,
                    correlationId,
                    now,
                    new Dictionary<string, string> { ["reason"] = "owner-recovery" }));
                context.EnqueueOutbox(new HostOutboxMessage(
                    $"owner-recovery:{challenge.Payload.ResponseId}",
                    "security.recovery",
                    "owner-password.reset",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["requestId"] = challenge.Payload.RequestId,
                        ["responseId"] = challenge.Payload.ResponseId
                    }));
                return Result.Success();
            });
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException and
            not StackOverflowException)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    public Result Cancel(RecoveryVerifiedChallenge challenge, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        AppendRecoveryImportAudit(
            executor,
            LoadOwnerId(executor) ?? "owner-recovery",
            AuditResult.Failed,
            "cancelled",
            challenge.Payload.RequestId,
            correlationId);
        return Result.Failure(ErrorCode.Cancelled);
    }

    public static string HashNonce(string nonce)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nonce)))
            .ToLowerInvariant();
    }

    private StoredRequestMatch MatchStoredRequest(
        IHostPersistenceCommandExecutor commands,
        RecoveryResponsePayload payload,
        DateTimeOffset now)
    {
        var request = commands.Query<RecoveryRequestRow>(
            """
            SELECT
                RequestId, NonceHash, CustomerId, DeploymentId, DeviceId,
                RecoveryPublicKeyHash, VendorKeyId, PayloadHash, IssuedAtUtc, ExpiresAtUtc,
                ConsumedAtUtc, ResponseId
            FROM RecoveryRequests
            WHERE RequestId = @RequestId;
            """,
            new { payload.RequestId }).SingleOrDefault();
        if (request is null)
        {
            return StoredRequestMatch.Failure("request-missing");
        }

        if (request.ConsumedAtUtc is not null ||
            request.ResponseId is not null ||
            commands.QueryScalar<int>(
                "SELECT COUNT(*) FROM RecoveryRequests WHERE ResponseId = @ResponseId;",
                new { payload.ResponseId }) > 0)
        {
            return StoredRequestMatch.Failure("replay");
        }

        if (!RecoveryContractValidation.TryParseUtcZ(request.IssuedAtUtc, out var requestIssuedAt) ||
            !RecoveryContractValidation.TryParseUtcZ(request.ExpiresAtUtc, out var requestExpiresAt) ||
            !RecoveryContractValidation.TryParseUtcZ(payload.IssuedAt, out var responseIssuedAt) ||
            responseIssuedAt < requestIssuedAt ||
            responseIssuedAt > requestExpiresAt ||
            now.ToUniversalTime() < requestIssuedAt.AddMinutes(-5))
        {
            return StoredRequestMatch.Failure("request-expired");
        }

        if (!StringComparer.Ordinal.Equals(request.NonceHash, HashNonce(payload.Nonce)) ||
            !StringComparer.Ordinal.Equals(request.CustomerId, payload.CustomerId) ||
            !StringComparer.Ordinal.Equals(request.DeploymentId, payload.DeploymentId) ||
            !StringComparer.Ordinal.Equals(request.DeviceId, payload.DeviceId) ||
            !StringComparer.Ordinal.Equals(request.RecoveryPublicKeyHash, payload.RecoveryPublicKeyHash) ||
            !StringComparer.Ordinal.Equals(request.VendorKeyId, payload.KeyId) ||
            !StringComparer.Ordinal.Equals(currentAuthority.CustomerId, payload.CustomerId) ||
            !StringComparer.Ordinal.Equals(currentAuthority.DeploymentId, payload.DeploymentId) ||
            !StringComparer.Ordinal.Equals(currentAuthority.DeviceId, payload.DeviceId) ||
            !StringComparer.Ordinal.Equals(currentRecoveryPublicKeyHash, payload.RecoveryPublicKeyHash))
        {
            return StoredRequestMatch.Failure("binding-mismatch");
        }

        var reconstructedRequest = new RecoveryRequestPayload(
            2,
            Dobby.Security.Trust.ArtifactSignaturePurposes.OwnerRecoveryRequest,
            request.RequestId,
            payload.Nonce,
            request.CustomerId,
            request.DeploymentId,
            request.DeviceId,
            request.RecoveryPublicKeyHash,
            request.IssuedAtUtc,
            request.ExpiresAtUtc,
            request.VendorKeyId);
        var reconstructedHash = Convert.ToHexString(
            SHA256.HashData(RecoveryCanonicalJsonWriter.WriteRequestPayload(reconstructedRequest)))
            .ToLowerInvariant();
        if (!StringComparer.Ordinal.Equals(request.PayloadHash, reconstructedHash))
        {
            return StoredRequestMatch.Failure("request-integrity-fail");
        }

        return StoredRequestMatch.Success();
    }

    private void AppendRecoveryImportAudit(
        IHostPersistenceCommandExecutor commands,
        string target,
        AuditResult result,
        string outcome,
        string requestId,
        CorrelationId correlationId)
    {
        auditTrail.AppendInScope(
            commands,
            CreateRecoveryImportAudit(target, result, outcome, requestId, correlationId));
    }

    private AuditEvent CreateRecoveryImportAudit(
        string target,
        AuditResult result,
        string outcome,
        string requestId,
        CorrelationId correlationId)
    {
        return new AuditEvent(
            AuditEventName.RecoveryImport,
            "system",
            target,
            result,
            correlationId,
            timeProvider.GetUtcNow(),
            new Dictionary<string, string>
            {
                ["outcome"] = outcome,
                ["requestId"] = requestId
            });
    }

    private static string? LoadOwnerId(IHostPersistenceCommandExecutor commands)
    {
        var rows = commands.Query<string>(
            "SELECT UserId FROM Users WHERE IsOwner = 1 AND IsActive = 1;");
        return rows.Count == 1 ? rows[0] : null;
    }

    private sealed class RecoveryRequestRow
    {
        public string RequestId { get; init; } = string.Empty;
        public string NonceHash { get; init; } = string.Empty;
        public string CustomerId { get; init; } = string.Empty;
        public string DeploymentId { get; init; } = string.Empty;
        public string DeviceId { get; init; } = string.Empty;
        public string RecoveryPublicKeyHash { get; init; } = string.Empty;
        public string VendorKeyId { get; init; } = string.Empty;
        public string PayloadHash { get; init; } = string.Empty;
        public string IssuedAtUtc { get; init; } = string.Empty;
        public string ExpiresAtUtc { get; init; } = string.Empty;
        public string? ConsumedAtUtc { get; init; }
        public string? ResponseId { get; init; }
    }

    private sealed record StoredRequestMatch(bool IsSuccess, string Outcome)
    {
        public static StoredRequestMatch Success() => new(true, "success");

        public static StoredRequestMatch Failure(string outcome) => new(false, outcome);
    }
}

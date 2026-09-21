using System.IO;
using System.Security.Cryptography;
using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed record RecoveryRequestExportResult(string FilePath);

public sealed class RecoveryRequestExportCoordinator(
    RecoveryRequestExporter exporter,
    IHostMutationUnitOfWork mutationUnitOfWork,
    TimeProvider timeProvider)
{
    public Result<RecoveryRequestExportResult> Export(
        string outputDirectory,
        CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var request = exporter.CreateRequest();
        if (!request.IsSuccess)
        {
            return Result.Failure<RecoveryRequestExportResult>(request.ErrorCode!.Value);
        }

        Directory.CreateDirectory(outputDirectory);
        var filePath = Path.Combine(
            outputDirectory,
            request.Value.Payload.RequestId + ".dobbyrecoveryrequest");
        try
        {
            using (var stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(RecoveryRequestExporter.Serialize(request.Value));
            }

            var persisted = mutationUnitOfWork.ExecuteResult(context =>
            {
                var payload = request.Value.Payload;
                context.Commands.Execute(
                    """
                    INSERT INTO RecoveryRequests(
                        RequestId, NonceHash, CustomerId, DeploymentId, DeviceId,
                        RecoveryPublicKeyHash, VendorKeyId, PayloadHash,
                        IssuedAtUtc, ExpiresAtUtc, CreatedAtUtc, ConsumedAtUtc, ResponseId)
                    VALUES(
                        @RequestId, @NonceHash, @CustomerId, @DeploymentId, @DeviceId,
                        @RecoveryPublicKeyHash, @VendorKeyId, @PayloadHash,
                        @IssuedAtUtc, @ExpiresAtUtc, @CreatedAtUtc, NULL, NULL);
                    """,
                    new
                    {
                        payload.RequestId,
                        NonceHash = HashText(payload.Nonce),
                        payload.CustomerId,
                        payload.DeploymentId,
                        payload.DeviceId,
                        payload.RecoveryPublicKeyHash,
                        payload.VendorKeyId,
                        PayloadHash = Convert.ToHexString(
                            SHA256.HashData(RecoveryCanonicalJsonWriter.WriteRequestPayload(payload)))
                            .ToLowerInvariant(),
                        IssuedAtUtc = payload.IssuedAt,
                        ExpiresAtUtc = payload.ExpiresAt,
                        CreatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime().ToString("O")
                    });
                context.AppendAudit(new AuditEvent(
                    AuditEventName.RecoveryExport,
                    "system",
                    "owner-recovery-request",
                    AuditResult.Success,
                    correlationId,
                    timeProvider.GetUtcNow(),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["requestId"] = payload.RequestId,
                        // Non-reversible replay-correlation field required by
                        // docs/specs/session-lifecycle.md § Recovery audit; the raw nonce
                        // is never written to the audit trail.
                        ["nonceHash"] = HashText(payload.Nonce)
                    }));
                context.EnqueueOutbox(new HostOutboxMessage(
                    $"recovery-request:{payload.RequestId}",
                    "security.recovery",
                    "recovery.requested",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["requestId"] = payload.RequestId
                    },
                    correlationId.ToString()));
                return Result.Success();
            });
            if (!persisted.IsSuccess)
            {
                File.Delete(filePath);
                return Result.Failure<RecoveryRequestExportResult>(persisted.ErrorCode!.Value);
            }

            return Result.Success(new RecoveryRequestExportResult(filePath));
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            TryDeleteIncompleteFile(filePath);
            return Result.Failure<RecoveryRequestExportResult>(
                exception is IOException or UnauthorizedAccessException
                    ? ErrorCode.IoFailed
                    : ErrorCode.DatabaseUnavailable);
        }
    }

    private static string HashText(string value)
    {
        return Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private static void TryDeleteIncompleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

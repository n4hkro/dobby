using Dobby.Audit;
using Dobby.Core;
using Dobby.Security.Authentication;
using System.IO;

namespace Dobby.Host.Login;

public sealed record RecoveryRequestExportResult(string FilePath);

public sealed class RecoveryRequestExportCoordinator(
    RecoveryRequestExporter exporter,
    IAuditTrail auditTrail)
{
    public Result<RecoveryRequestExportResult> Export(string outputDirectory, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var request = exporter.CreateRequest();
        if (!request.IsSuccess)
        {
            return Result.Failure<RecoveryRequestExportResult>(request.ErrorCode!.Value);
        }

        Directory.CreateDirectory(outputDirectory);
        var filePath = Path.Combine(outputDirectory, request.Value.RequestId + ".dobbyrecoveryrequest");
        File.WriteAllText(filePath, RecoveryRequestExporter.Serialize(request.Value));

        auditTrail.Append(new AuditEvent(
            AuditEventName.RecoveryExport,
            "system",
            "owner-recovery-request",
            AuditResult.Success,
            correlationId,
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>
            {
                ["requestId"] = request.Value.RequestId
            }));

        return Result.Success(new RecoveryRequestExportResult(filePath));
    }
}

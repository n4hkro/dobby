using Dobby.Audit;
using Dobby.Core;
using Dobby.Diagnostics;
using System.IO;

namespace Dobby.Host;

public sealed class ShellDiagnosticsExportCoordinator(
    DiagnosticsExportPackageBuilder packageBuilder,
    IAuditTrail auditTrail,
    TimeProvider timeProvider)
{
    public Result<DiagnosticsExportPackage> Export(
        string actorId,
        string sourceDirectory,
        string outputZipPath,
        CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputZipPath);

        try
        {
            var package = packageBuilder.Build(sourceDirectory, outputZipPath);
            auditTrail.Append(new AuditEvent(
                AuditEventName.DiagnosticsExportCreated,
                actorId,
                "diagnostics.export",
                AuditResult.Success,
                correlationId,
                timeProvider.GetUtcNow(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["path"] = package.Path,
                    ["included"] = package.IncludedEntries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["excluded"] = package.ExcludedEntries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["uploaded"] = package.Uploaded.ToString().ToLowerInvariant()
                }));

            return Result.Success(package);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            auditTrail.Append(new AuditEvent(
                AuditEventName.DiagnosticsExportCreated,
                actorId,
                "diagnostics.export",
                AuditResult.Failed,
                correlationId,
                timeProvider.GetUtcNow(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["uploaded"] = "false"
                }));

            return Result.Failure<DiagnosticsExportPackage>(ErrorCode.IoFailed);
        }
    }
}

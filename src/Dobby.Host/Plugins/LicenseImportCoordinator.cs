using System.IO;
using Dobby.Audit;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Diagnostics;
using Dobby.Host.Login;
using Dobby.Licensing;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;

namespace Dobby.Host.Plugins;

public sealed class LicenseImportCoordinator(
    LicenseImportService importService,
    IInstalledPluginStateStore installedPluginStateStore,
    OperationAuthorizationPolicy authorizationPolicy,
    Func<string> currentDeviceFingerprintProvider,
    IAuditTrail auditTrail,
    TimeProvider timeProvider) : IPostLoginLicenseImportCancellation
{
    public void CancelLicenseImport()
    {
    }

    public LicenseImportResult ImportFile(string actorId, string path, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fileName = Path.GetFileName(path);
        AppendAudit(
            AuditEventName.LicenseImportAttempted,
            actorId,
            "unknown",
            AuditResult.Success,
            correlationId,
            "attempted");

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            var failure = LicenseImportResult.Failure(LicenseRejectReasons.InvalidLicenseFile, "file-read-failed");
            AppendRejected(actorId, "unknown", failure, correlationId);
            return failure;
        }
        catch (UnauthorizedAccessException)
        {
            var failure = LicenseImportResult.Failure(LicenseRejectReasons.InvalidLicenseFile, "file-read-failed");
            AppendRejected(actorId, "unknown", failure, correlationId);
            return failure;
        }

        var permissionGranted = HasImportPermission(actorId, correlationId);

        var result = importService.Import(new LicenseImportRequest(
            fileName,
            bytes,
            currentDeviceFingerprintProvider,
            FindInstalledPackageFingerprint,
            permissionGranted,
            correlationId.ToString(),
            timeProvider.GetUtcNow()));

        if (result.IsSuccess)
        {
            AppendAudit(
                AuditEventName.LicenseImportSucceeded,
                actorId,
                Target(result.PluginId),
                AuditResult.Success,
                correlationId,
                "succeeded",
                result.LifecycleState.ToString());
        }
        else
        {
            AppendRejected(actorId, Target(result.PluginId), result, correlationId);
        }

        return result;
    }

    private bool HasImportPermission(string actorId, CorrelationId correlationId)
    {
        var decision = authorizationPolicy.Evaluate(
            new OperationPermissionCapability(
                new OperationKey(BuiltInOperationTargets.PluginLicenseTargetId, OperationVerb.Import).ToString()),
            new PolicyContext(actorId, null, correlationId));
        return decision is { IsSuccess: true, Value.Allowed: true };
    }

    private string FindInstalledPackageFingerprint(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return string.Empty;
        }

        var snapshot = installedPluginStateStore.Snapshot();
        if (!snapshot.IsSuccess)
        {
            return string.Empty;
        }

        return snapshot.Value
            .SingleOrDefault(state => state.PluginId.Equals(pluginId, StringComparison.Ordinal))
            ?.PackageFingerprint ?? string.Empty;
    }

    private void AppendRejected(
        string actorId,
        string target,
        LicenseImportResult result,
        CorrelationId correlationId)
    {
        AppendAudit(
            AuditEventName.LicenseImportRejected,
            actorId,
            target,
            AuditResult.Denied,
            correlationId,
            result.ReasonCode);
    }

    private void AppendAudit(
        string eventName,
        string actorId,
        string target,
        AuditResult result,
        CorrelationId correlationId,
        string reasonCode,
        string? lifecycleState = null)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["reasonCode"] = reasonCode,
            ["diagnosticMarker"] = $"{eventName}:{reasonCode}"
        };
        if (!string.IsNullOrWhiteSpace(lifecycleState))
        {
            details["lifecycleState"] = lifecycleState;
        }

        auditTrail.Append(new AuditEvent(
            eventName,
            actorId,
            target,
            result,
            correlationId,
            timeProvider.GetUtcNow(),
            DiagnosticRedactor.RedactFields(details)));
    }

    private static string Target(string pluginId)
    {
        return string.IsNullOrWhiteSpace(pluginId) ? "license:unknown" : $"license:{pluginId}";
    }
}

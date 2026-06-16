using Dobby.Audit;
using Dobby.Core;
using Dobby.Diagnostics;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Packaging;
using System.IO;

namespace Dobby.Host.Plugins;

public sealed class PluginRuntimeAuditCoordinator(
    IAuditTrail auditTrail,
    TimeProvider timeProvider)
{
    public void AppendInstallAttempt(string actorId, string packagePath, CorrelationId correlationId)
    {
        Append(
            AuditEventName.PluginInstallAttempted,
            actorId,
            PackageTarget(packagePath),
            AuditResult.Success,
            correlationId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["packageName"] = Path.GetFileName(packagePath),
                ["diagnosticMarker"] = "plugin.install.attempted"
            });
    }

    public void AppendInstallVerified(string actorId, PluginPackageDescriptor descriptor, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        Append(
            AuditEventName.PluginInstallVerified,
            actorId,
            PluginTarget(descriptor.Manifest.PluginId),
            AuditResult.Success,
            correlationId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pluginId"] = descriptor.Manifest.PluginId,
                ["fingerprint"] = descriptor.Fingerprint,
                ["diagnosticMarker"] = "plugin.install.verified"
            });
    }

    public void AppendInstallRejected(
        string actorId,
        string? pluginId,
        IReadOnlyList<PluginPackageValidationReason> reasons,
        CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(reasons);

        var reason = reasons.FirstOrDefault() ??
            new PluginPackageValidationReason("validation-failed", "Plugin package validation failed.");
        var eventName = reason.Code switch
        {
            "signature-failed" => AuditEventName.PluginInstallSignatureFailed,
            "trust-failed" => AuditEventName.PluginInstallTrustFailed,
            _ => AuditEventName.PluginInstallRejected
        };

        Append(
            eventName,
            actorId,
            PluginTarget(pluginId ?? "unknown"),
            AuditResult.Denied,
            correlationId,
            ReasonDetails(pluginId, eventName, reason));
    }

    public void AppendActivationOutcomes(
        string actorId,
        IReadOnlyList<PluginActivationOutcome> outcomes,
        CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        foreach (var outcome in outcomes)
        {
            var eventName = outcome.Status switch
            {
                PluginActivationStatus.Skipped => AuditEventName.PluginActivationSkipped,
                PluginActivationStatus.Rejected => AuditEventName.PluginActivationRejected,
                PluginActivationStatus.Faulted => AuditEventName.PluginRuntimeFaulted,
                _ => null
            };
            if (eventName is null)
            {
                continue;
            }

            Append(
                eventName,
                actorId,
                PluginTarget(outcome.PluginId),
                outcome.Status == PluginActivationStatus.Rejected ? AuditResult.Denied : AuditResult.Failed,
                correlationId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["pluginId"] = outcome.PluginId,
                    ["status"] = outcome.Status.ToString(),
                    ["reasonCode"] = NormalizeReason(outcome.Reason),
                    ["diagnosticMarker"] = $"{eventName}:{NormalizeReason(outcome.Reason)}"
                });
        }
    }

    private void Append(
        string eventName,
        string actorId,
        string target,
        AuditResult result,
        CorrelationId correlationId,
        IReadOnlyDictionary<string, string> details)
    {
        auditTrail.Append(new AuditEvent(
            eventName,
            actorId,
            target,
            result,
            correlationId,
            timeProvider.GetUtcNow(),
            DiagnosticRedactor.RedactFields(details)));
    }

    private static IReadOnlyDictionary<string, string> ReasonDetails(
        string? pluginId,
        string eventName,
        PluginPackageValidationReason reason)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pluginId"] = pluginId ?? "unknown",
            ["reasonCode"] = reason.Code,
            ["diagnosticMarker"] = $"{eventName}:{reason.Code}"
        };
        if (!string.IsNullOrWhiteSpace(reason.Path))
        {
            details["reasonPath"] = reason.Path;
        }

        return details;
    }

    private static string NormalizeReason(string reason)
    {
        var separator = reason.IndexOf(':', StringComparison.Ordinal);
        return separator < 0
            ? (string.IsNullOrWhiteSpace(reason) ? "none" : reason)
            : reason[..separator].Trim();
    }

    private static string PackageTarget(string packagePath)
    {
        return $"package:{Path.GetFileName(packagePath)}";
    }

    private static string PluginTarget(string pluginId)
    {
        return $"plugin:{pluginId}";
    }
}

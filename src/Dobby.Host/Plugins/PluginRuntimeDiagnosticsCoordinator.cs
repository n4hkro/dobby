using Dobby.Diagnostics;
using Dobby.Core;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Packaging;

namespace Dobby.Host.Plugins;

public sealed class PluginRuntimeDiagnosticsCoordinator(
    Action<PluginRuntimeDiagnosticEntry> record)
{
    private const string HostRuntimePluginId = "host";
    private readonly Action<PluginRuntimeDiagnosticEntry> record = record ??
        throw new ArgumentNullException(nameof(record));

    public void RecordActivationOutcome(PluginActivationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.InvocationKind == PluginInvocationOutcomeKind.HostInfrastructureFailure)
        {
            Emit(
                HostRuntimePluginId,
                "plugin.runtime.host-failed:plugin.activation",
                $"pluginId={outcome.PluginId}; reason={NormalizeReason(outcome.Reason)}");
            return;
        }

        var markerPrefix = outcome.Status switch
        {
            PluginActivationStatus.Skipped => "plugin.runtime.skipped",
            PluginActivationStatus.Rejected => "plugin.runtime.rejected",
            PluginActivationStatus.Faulted => "plugin.runtime.faulted",
            _ => null
        };
        if (markerPrefix is null)
        {
            return;
        }

        var reasonCode = NormalizeReason(outcome.Reason);
        Emit(outcome.PluginId, $"{markerPrefix}:{reasonCode}", outcome.Reason);
    }

    public void RecordActivationOutcomes(IEnumerable<PluginActivationOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        foreach (var outcome in outcomes)
        {
            RecordActivationOutcome(outcome);
        }
    }

    public void RecordActivationCancelled(PluginActivationRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        Emit(
            HostRuntimePluginId,
            "plugin.runtime.cancelled",
            $"activated={run.ActivatedPluginIds.Count}");
    }

    public void RecordRuntimeFailure(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Emit(HostRuntimePluginId, $"plugin.runtime.failed:{NormalizeReason(reason)}", reason);
    }

    public void RecordEventHandlerFault(string pluginId, Type eventType, ErrorCode errorCode)
    {
        RecordEventHandlerOutcome(
            pluginId,
            eventType,
            errorCode,
            PluginInvocationOutcomeKind.PluginException,
            correlationId: null);
    }

    public void RecordEventHandlerOutcome(
        string pluginId,
        Type eventType,
        ErrorCode errorCode,
        PluginInvocationOutcomeKind outcomeKind,
        string? correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(eventType);

        RecordInvocationOutcome(
            pluginId,
            "plugin.event.handler",
            $"event={eventType.FullName ?? eventType.Name}; error={errorCode}",
            outcomeKind,
            correlationId);
    }

    public void RecordPageFault(string pluginId, string routeKey, string operation, ErrorCode errorCode)
    {
        RecordPageOutcome(
            pluginId,
            routeKey,
            operation,
            errorCode,
            PluginExceptionBoundary.ClassifyResult(isSuccess: false, errorCode),
            correlationId: null);
    }

    public void RecordPageOutcome(
        string pluginId,
        string routeKey,
        string operation,
        ErrorCode errorCode,
        PluginInvocationOutcomeKind outcomeKind,
        string? correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        RecordInvocationOutcome(
            pluginId,
            $"plugin.page.{NormalizeReason(operation)}",
            $"route={routeKey}; error={errorCode}",
            outcomeKind,
            correlationId);
    }

    public void RecordPageActionFault(string pluginId, string routeKey, string actionId, ErrorCode errorCode)
    {
        RecordPageActionOutcome(
            pluginId,
            routeKey,
            actionId,
            errorCode,
            PluginExceptionBoundary.ClassifyResult(isSuccess: false, errorCode),
            correlationId: null);
    }

    public void RecordPageActionOutcome(
        string pluginId,
        string routeKey,
        string actionId,
        ErrorCode errorCode,
        PluginInvocationOutcomeKind outcomeKind,
        string? correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        RecordInvocationOutcome(
            pluginId,
            "plugin.page.command",
            $"route={routeKey}; command={actionId}; error={errorCode}",
            outcomeKind,
            correlationId);
    }

    public void RecordInstallRejected(string? pluginId, IReadOnlyList<PluginPackageValidationReason> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);

        if (reasons.Count == 0)
        {
            Emit(pluginId ?? HostRuntimePluginId, "plugin.install.rejected:none", "Plugin package was rejected.");
            return;
        }

        var reason = reasons[0];
        Emit(
            pluginId ?? HostRuntimePluginId,
            $"plugin.install.rejected:{NormalizeReason(reason.Code)}",
            FormatPackageValidationReason(reason));
    }

    private void Emit(string pluginId, string marker, string details)
    {
        record(new PluginRuntimeDiagnosticEntry(
            pluginId,
            marker,
            DiagnosticRedactor.Redact(details)));
    }

    private void RecordInvocationOutcome(
        string pluginId,
        string operationMarker,
        string details,
        PluginInvocationOutcomeKind outcomeKind,
        string? correlationId)
    {
        if (outcomeKind is
            PluginInvocationOutcomeKind.Success or
            PluginInvocationOutcomeKind.ExpectedRejection or
            PluginInvocationOutcomeKind.AuthorizationOrLicenseDenied or
            PluginInvocationOutcomeKind.Cancelled)
        {
            return;
        }

        var correlation = string.IsNullOrWhiteSpace(correlationId)
            ? "none"
            : correlationId;
        if (outcomeKind == PluginInvocationOutcomeKind.HostInfrastructureFailure)
        {
            Emit(
                HostRuntimePluginId,
                $"plugin.runtime.host-failed:{operationMarker}",
                $"pluginId={pluginId}; {details}; correlation={correlation}");
            return;
        }

        Emit(
            pluginId,
            $"plugin.runtime.defect:{operationMarker}.{outcomeKind.ToString().ToLowerInvariant()}",
            $"{details}; correlation={correlation}");
    }

    private static string FormatPackageValidationReason(PluginPackageValidationReason reason)
    {
        return string.IsNullOrWhiteSpace(reason.Path)
            ? reason.Message
            : $"{reason.Path}: {reason.Message}";
    }

    private static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "none";
        }

        var separator = reason.IndexOfAny([':', ' ', '\t', '\r', '\n']);
        return separator < 0
            ? reason.Trim()
            : reason[..separator].Trim();
    }
}

public sealed record PluginRuntimeDiagnosticEntry(
    string PluginId,
    string Marker,
    string Details);

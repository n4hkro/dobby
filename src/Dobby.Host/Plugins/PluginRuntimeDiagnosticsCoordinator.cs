using Dobby.Diagnostics;
using Dobby.PluginFramework.Activation;

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

    private void Emit(string pluginId, string marker, string details)
    {
        record(new PluginRuntimeDiagnosticEntry(
            pluginId,
            marker,
            DiagnosticRedactor.Redact(details)));
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

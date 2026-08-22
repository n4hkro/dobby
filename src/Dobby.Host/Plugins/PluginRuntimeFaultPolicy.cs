using Dobby.PluginFramework.Activation;

namespace Dobby.Host.Plugins;

public sealed record PluginRuntimeFaultState(
    string PluginId,
    int DefectCount,
    bool IsQuarantined);

public sealed record PluginRuntimeFaultDecision(
    PluginRuntimeFaultState State,
    bool QuarantineStarted);

public sealed class PluginRuntimeFaultPolicy(int quarantineThreshold = 3)
{
    private readonly object gate = new();
    private readonly Dictionary<string, PluginRuntimeFaultState> states = new(StringComparer.Ordinal);

    public int QuarantineThreshold { get; } = quarantineThreshold is >= 1 and <= 100
        ? quarantineThreshold
        : throw new ArgumentOutOfRangeException(nameof(quarantineThreshold));

    public PluginRuntimeFaultDecision Observe(string pluginId, PluginInvocationOutcomeKind outcomeKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        lock (gate)
        {
            var current = states.TryGetValue(pluginId, out var existing)
                ? existing
                : new PluginRuntimeFaultState(pluginId, 0, IsQuarantined: false);
            if (current.IsQuarantined)
            {
                return new PluginRuntimeFaultDecision(current, QuarantineStarted: false);
            }

            if (outcomeKind == PluginInvocationOutcomeKind.Success)
            {
                var reset = current with { DefectCount = 0 };
                states[pluginId] = reset;
                return new PluginRuntimeFaultDecision(reset, QuarantineStarted: false);
            }

            if (!PluginExceptionBoundary.IsPluginDefect(outcomeKind))
            {
                states[pluginId] = current;
                return new PluginRuntimeFaultDecision(current, QuarantineStarted: false);
            }

            var count = checked(current.DefectCount + 1);
            var quarantined = count >= QuarantineThreshold;
            var next = current with { DefectCount = count, IsQuarantined = quarantined };
            states[pluginId] = next;
            return new PluginRuntimeFaultDecision(
                next,
                QuarantineStarted: quarantined && !current.IsQuarantined);
        }
    }

    public PluginRuntimeFaultState Snapshot(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        lock (gate)
        {
            return states.TryGetValue(pluginId, out var state)
                ? state
                : new PluginRuntimeFaultState(pluginId, 0, IsQuarantined: false);
        }
    }

    public bool IsQuarantined(string pluginId) => Snapshot(pluginId).IsQuarantined;

}

using System.Collections.Concurrent;

namespace Dobby.Host.Plugins;

internal sealed class PluginInstallConcurrencyGate
{
    private readonly SemaphoreSlim capacity;
    private readonly ConcurrentDictionary<string, PluginGateState> pluginGates = new(StringComparer.Ordinal);
    private int waitingForCapacity;

    public PluginInstallConcurrencyGate(int maximumConcurrentOperations)
    {
        if (maximumConcurrentOperations is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrentOperations));
        }

        capacity = new SemaphoreSlim(maximumConcurrentOperations, maximumConcurrentOperations);
    }

    public Lease Enter(string pluginId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        Interlocked.Increment(ref waitingForCapacity);
        try
        {
            capacity.Wait(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref waitingForCapacity);
        }

        var state = pluginGates.GetOrAdd(pluginId, static _ => new PluginGateState());
        Interlocked.Increment(ref state.Waiting);
        try
        {
            state.Gate.Wait(cancellationToken);
        }
        catch
        {
            capacity.Release();
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref state.Waiting);
        }

        Interlocked.Increment(ref state.Active);
        return new Lease(this, state);
    }

    internal PluginInstallConcurrencySnapshot Snapshot(string pluginId)
    {
        var state = pluginGates.GetValueOrDefault(pluginId);
        return new PluginInstallConcurrencySnapshot(
            ActiveForPlugin: state is null ? 0 : Volatile.Read(ref state.Active),
            WaitingForPlugin: state is null ? 0 : Volatile.Read(ref state.Waiting),
            WaitingForCapacity: Volatile.Read(ref waitingForCapacity));
    }

    private void Exit(PluginGateState state)
    {
        Interlocked.Decrement(ref state.Active);
        state.Gate.Release();
        capacity.Release();
    }

    internal sealed class Lease : IDisposable
    {
        private PluginInstallConcurrencyGate? owner;
        private readonly PluginGateState state;

        internal Lease(PluginInstallConcurrencyGate owner, PluginGateState state)
        {
            this.owner = owner;
            this.state = state;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)?.Exit(state);
        }
    }

    internal sealed class PluginGateState
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int Active;
        public int Waiting;
    }
}

internal sealed record PluginInstallConcurrencySnapshot(
    int ActiveForPlugin,
    int WaitingForPlugin,
    int WaitingForCapacity);

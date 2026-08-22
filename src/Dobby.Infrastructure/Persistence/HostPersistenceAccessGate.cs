using System.Collections.Concurrent;
using System.IO;

namespace Dobby.Infrastructure.Persistence;

internal static class HostPersistenceAccessGate
{
    private static readonly ConcurrentDictionary<string, GateState> Gates =
        new(StringComparer.OrdinalIgnoreCase);

    public static IDisposable EnterShared(string databasePath)
    {
        var state = Resolve(databasePath);
        lock (state.Sync)
        {
            while (state.WriterActive || state.WaitingWriters > 0)
            {
                Monitor.Wait(state.Sync);
            }
            state.ActiveReaders++;
        }
        return new Lease(state, exclusive: false);
    }

    public static IDisposable EnterExclusive(string databasePath, CancellationToken cancellationToken)
    {
        var state = Resolve(databasePath);
        lock (state.Sync)
        {
            state.WaitingWriters++;
            try
            {
                while (state.WriterActive || state.ActiveReaders > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Monitor.Wait(state.Sync, TimeSpan.FromMilliseconds(100));
                }
                state.WriterActive = true;
            }
            finally
            {
                state.WaitingWriters--;
            }
        }
        return new Lease(state, exclusive: true);
    }

    private static GateState Resolve(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var canonical = Path.GetFullPath(Environment.ExpandEnvironmentVariables(databasePath));
        return Gates.GetOrAdd(canonical, static _ => new GateState());
    }

    private sealed class GateState
    {
        public object Sync { get; } = new();

        public int ActiveReaders { get; set; }

        public int WaitingWriters { get; set; }

        public bool WriterActive { get; set; }
    }

    private sealed class Lease(GateState state, bool exclusive) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            lock (state.Sync)
            {
                if (exclusive)
                {
                    state.WriterActive = false;
                }
                else
                {
                    state.ActiveReaders--;
                }
                Monitor.PulseAll(state.Sync);
            }
        }
    }
}

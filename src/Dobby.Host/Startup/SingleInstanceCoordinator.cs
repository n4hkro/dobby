using System.Threading;

namespace Dobby.Host.Startup;

public sealed class SingleInstanceCoordinator : ISingleInstanceCoordinator, IDisposable
{
    private readonly string mutexName;
    private readonly EventWaitHandle activationEvent;
    private Mutex? mutex;
    private bool ownsMutex;

    public SingleInstanceCoordinator()
        : this("Local\\Dobby.Host", "Local\\Dobby.Host.Activate")
    {
    }

    public SingleInstanceCoordinator(string mutexName, string activationEventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        ArgumentException.ThrowIfNullOrWhiteSpace(activationEventName);

        this.mutexName = mutexName;
        activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, activationEventName);
    }

    public SingleInstanceAcquireResult Acquire()
    {
        if (mutex is not null)
        {
            return ownsMutex
                ? SingleInstanceAcquireResult.FirstInstance()
                : SingleInstanceAcquireResult.SecondInstance();
        }

        mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        ownsMutex = createdNew;
        return ownsMutex
            ? SingleInstanceAcquireResult.FirstInstance()
            : SingleInstanceAcquireResult.SecondInstance();
    }

    public void HandOffToFirstInstance()
    {
        activationEvent.Set();
    }

    public void Dispose()
    {
        if (ownsMutex)
        {
            mutex?.ReleaseMutex();
        }

        mutex?.Dispose();
        activationEvent.Dispose();
    }
}

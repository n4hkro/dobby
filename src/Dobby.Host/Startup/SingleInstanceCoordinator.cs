using System.Threading;

namespace Dobby.Host.Startup;

public sealed class SingleInstanceCoordinator : ISingleInstanceCoordinator, IDisposable
{
    private static readonly TimeSpan DefaultExitingFirstInstanceWait = TimeSpan.FromMilliseconds(750);
    private readonly string mutexName;
    private readonly TimeSpan exitingFirstInstanceWait;
    private readonly EventWaitHandle activationEvent;
    private Mutex? mutex;
    private RegisteredWaitHandle? activationRegistration;
    private bool ownsMutex;
    private int disposed;

    public SingleInstanceCoordinator()
        : this("Local\\Dobby.Host", "Local\\Dobby.Host.Activate")
    {
    }

    public SingleInstanceCoordinator(string mutexName, string activationEventName)
        : this(mutexName, activationEventName, DefaultExitingFirstInstanceWait)
    {
    }

    public SingleInstanceCoordinator(
        string mutexName,
        string activationEventName,
        TimeSpan exitingFirstInstanceWait)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        ArgumentException.ThrowIfNullOrWhiteSpace(activationEventName);
        if (exitingFirstInstanceWait < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(exitingFirstInstanceWait));
        }

        this.mutexName = mutexName;
        this.exitingFirstInstanceWait = exitingFirstInstanceWait;
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

        mutex = new Mutex(initiallyOwned: false, mutexName);
        try
        {
            ownsMutex = mutex.WaitOne(exitingFirstInstanceWait);
        }
        catch (AbandonedMutexException)
        {
            ownsMutex = true;
        }

        return ownsMutex
            ? SingleInstanceAcquireResult.FirstInstance()
            : SingleInstanceAcquireResult.SecondInstance();
    }

    public void HandOffToFirstInstance()
    {
        activationEvent.Set();
    }

    public IDisposable StartActivationListener(Action activationRequested)
    {
        ArgumentNullException.ThrowIfNull(activationRequested);
        if (!ownsMutex)
        {
            throw new InvalidOperationException("Only the first instance can listen for activation requests.");
        }

        activationRegistration?.Unregister(null);
        var callback = new ActivationCallback(activationRequested);
        activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            activationEvent,
            static (state, timedOut) =>
            {
                if (!timedOut && state is ActivationCallback callback)
                {
                    callback.Invoke();
                }
            },
            callback,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);

        return new ActivationListenerRegistration(this, activationRegistration);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        activationRegistration?.Unregister(null);
        activationRegistration = null;
        if (ownsMutex)
        {
            mutex?.ReleaseMutex();
            ownsMutex = false;
        }

        mutex?.Dispose();
        mutex = null;
        activationEvent.Dispose();
    }

    private void StopActivationListener(RegisteredWaitHandle registration)
    {
        if (ReferenceEquals(activationRegistration, registration))
        {
            activationRegistration.Unregister(null);
            activationRegistration = null;
        }
        else
        {
            registration.Unregister(null);
        }
    }

    private sealed class ActivationListenerRegistration(
        SingleInstanceCoordinator owner,
        RegisteredWaitHandle registration) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.StopActivationListener(registration);
            }
        }
    }

    private sealed class ActivationCallback(Action activationRequested)
    {
        public void Invoke()
        {
            try
            {
                activationRequested();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
            }
        }
    }
}

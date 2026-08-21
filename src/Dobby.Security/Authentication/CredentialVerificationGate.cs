namespace Dobby.Security.Authentication;

public sealed class CredentialVerificationGate
{
    private readonly SemaphoreSlim semaphore;

    public CredentialVerificationGate(int maximumConcurrency)
    {
        if (maximumConcurrency is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
        }

        semaphore = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
    }

    public IDisposable? TryAcquire()
    {
        return semaphore.Wait(0) ? new Lease(semaphore) : null;
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}

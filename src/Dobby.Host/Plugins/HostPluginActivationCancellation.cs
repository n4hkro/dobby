using Dobby.Contracts.BackgroundOperations;
using Dobby.Host.Login;

namespace Dobby.Host.Plugins;

public sealed class HostPluginActivationCancellation : IPostLoginPluginActivationCancellation, IBackgroundOperationCancellation, IDisposable
{
    private CancellationTokenSource source = new();

    public CancellationToken Token => source.Token;

    public CancellationToken CancellationToken => Token;

    public void Reset()
    {
        if (!source.IsCancellationRequested)
        {
            return;
        }

        source.Dispose();
        source = new CancellationTokenSource();
    }

    public void CancelPostLoginActivation()
    {
        if (!source.IsCancellationRequested)
        {
            source.Cancel();
        }
    }

    public void Dispose()
    {
        source.Dispose();
    }
}

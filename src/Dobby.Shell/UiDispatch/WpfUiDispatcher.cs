using System.Windows.Threading;
using Dobby.Messaging.UiDispatch;

namespace Dobby.Shell.UiDispatch;

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public bool IsOnUiThread => dispatcher.CheckAccess();

    public Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return IsOnUiThread
            ? action()
            : dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    public Task<T> InvokeAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return IsOnUiThread
            ? action()
            : dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = dispatcher.BeginInvoke(action);
    }
}

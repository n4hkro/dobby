namespace Dobby.Messaging.UiDispatch;

public interface IUiDispatcher
{
    bool IsOnUiThread { get; }

    Task InvokeAsync(Func<Task> action);

    Task<T> InvokeAsync<T>(Func<Task<T>> action);

    void Post(Action action);
}

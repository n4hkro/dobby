using Dobby.Host.Login;
using Dobby.Host.Plugins;
using Dobby.Shell.WorkSessions;

namespace Dobby.Host;

public sealed record PreparedLoginRuntime(
    HostLoginRuntime LoginRuntime,
    WorkSessionRegistry WorkSessionRegistry,
    WorkSessionCloseCoordinator WorkSessionCloseCoordinator) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await LoginRuntime.DisposeAsync().ConfigureAwait(false);
    }
}

public sealed record PreparedLoginEntryRuntime(
    HostPreLoginRuntime PreLoginRuntime,
    Task<PreparedLoginRuntime?> ShellRuntimeTask);

internal static class LoginRuntimePreparer
{
    public static async Task<PreparedLoginEntryRuntime?> InitializeLoginEntryRuntimeAsync(LoginWindow loginWindow)
    {
        try
        {
            var preLoginRuntime = await Task.Run(() => HostLoginRuntimeFactory.CreatePreLogin(applyPreLoginMigrations: false))
                .ConfigureAwait(false);
            var attached = false;
            await loginWindow.Dispatcher.InvokeAsync(() =>
            {
                if (!loginWindow.IsVisible)
                {
                    return;
                }

                loginWindow.AttachRuntime(preLoginRuntime);
                attached = true;
            }).Task.ConfigureAwait(false);

            return attached
                ? new PreparedLoginEntryRuntime(preLoginRuntime, InitializeShellRuntimeAsync())
                : null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            try
            {
                await loginWindow.Dispatcher.InvokeAsync(() =>
                {
                    if (loginWindow.IsVisible)
                    {
                        loginWindow.ShowRuntimeInitializationFailure();
                    }
                }).Task.ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (loginWindow.Dispatcher.HasShutdownStarted)
            {
            }
            catch (TaskCanceledException)
            {
            }

            return null;
        }
    }

    public static async Task<PreparedLoginRuntime?> InitializeLoginRuntimeAsync(LoginWindow loginWindow)
    {
        PreparedLoginRuntime? preparedRuntime = null;
        try
        {
            preparedRuntime = await InitializeShellRuntimeAsync().ConfigureAwait(false);
            if (preparedRuntime is null)
            {
                return null;
            }

            var attached = false;
            await loginWindow.Dispatcher.InvokeAsync(() =>
            {
                if (!loginWindow.IsVisible)
                {
                    return;
                }

                loginWindow.AttachRuntime(preparedRuntime.LoginRuntime);
                attached = true;
            }).Task.ConfigureAwait(false);

            if (attached)
            {
                return preparedRuntime;
            }

            await preparedRuntime.DisposeAsync().ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            if (preparedRuntime is not null)
            {
                await preparedRuntime.DisposeAsync().ConfigureAwait(false);
            }

            try
            {
                await loginWindow.Dispatcher.InvokeAsync(() =>
                {
                    if (loginWindow.IsVisible)
                    {
                        loginWindow.ShowRuntimeInitializationFailure();
                    }
                }).Task.ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (loginWindow.Dispatcher.HasShutdownStarted)
            {
            }
            catch (TaskCanceledException)
            {
            }

            return null;
        }
    }

    private static async Task<PreparedLoginRuntime?> InitializeShellRuntimeAsync()
    {
        PreparedLoginRuntime? preparedRuntime = null;
        try
        {
            preparedRuntime = await Task.Run(() =>
            {
                var workSessionRegistry = new WorkSessionRegistry();
                var loginRuntime = HostLoginRuntimeFactory.Create(
                    pluginWorkSessions: new ShellPluginWorkSessionService(workSessionRegistry),
                    applyPreLoginMigrations: false);
                return new PreparedLoginRuntime(
                    loginRuntime,
                    workSessionRegistry,
                    new WorkSessionCloseCoordinator(workSessionRegistry));
            }).ConfigureAwait(false);
            return preparedRuntime;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            if (preparedRuntime is not null)
            {
                await preparedRuntime.DisposeAsync().ConfigureAwait(false);
            }

            return null;
        }
    }

    public static async Task DisposeWhenReadyAsync(Task<PreparedLoginRuntime?>? preparedRuntimeTask)
    {
        if (preparedRuntimeTask is null)
        {
            return;
        }

        try
        {
            var preparedRuntime = await preparedRuntimeTask.ConfigureAwait(false);
            if (preparedRuntime is not null)
            {
                await preparedRuntime.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }

    public static async Task DisposeWhenReadyAsync(Task<PreparedLoginEntryRuntime?>? preparedRuntimeTask)
    {
        if (preparedRuntimeTask is null)
        {
            return;
        }

        try
        {
            var preparedRuntime = await preparedRuntimeTask.ConfigureAwait(false);
            if (preparedRuntime is not null)
            {
                await DisposeWhenReadyAsync(preparedRuntime.ShellRuntimeTask).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }

    public static void DisposeWhenReadyWithoutBlockingShutdown(Task<PreparedLoginRuntime?>? preparedRuntimeTask)
    {
        if (preparedRuntimeTask is null)
        {
            return;
        }

        _ = DisposeWhenReadyAsync(preparedRuntimeTask);
    }

    public static void DisposeWhenReadyWithoutBlockingShutdown(Task<PreparedLoginEntryRuntime?>? preparedRuntimeTask)
    {
        if (preparedRuntimeTask is null)
        {
            return;
        }

        _ = DisposeWhenReadyAsync(preparedRuntimeTask);
    }
}

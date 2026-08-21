using Dobby.Host.Login;
using Dobby.Host.Plugins;
using Dobby.Security.Authentication;
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
    HostPreLoginRuntime PreLoginRuntime);

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
                ? new PreparedLoginEntryRuntime(preLoginRuntime)
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

    public static Task<PreparedLoginRuntime?> InitializeAuthenticatedRuntimeAsync(
        SessionRecord authenticatedSession)
    {
        ArgumentNullException.ThrowIfNull(authenticatedSession);
        return InitializeAuthenticatedRuntimeCoreAsync(authenticatedSession);
    }

    private static async Task<PreparedLoginRuntime?> InitializeAuthenticatedRuntimeCoreAsync(
        SessionRecord authenticatedSession)
    {
        PreparedLoginRuntime? preparedRuntime = null;
        try
        {
            preparedRuntime = await Task.Run(() =>
            {
                var workSessionRegistry = new WorkSessionRegistry();
                var loginRuntime = HostLoginRuntimeFactory.CreateAuthenticated(
                    authenticatedSession,
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

    public static async Task ObserveWhenReadyAsync(Task<PreparedLoginEntryRuntime?>? preparedRuntimeTask)
    {
        if (preparedRuntimeTask is null)
        {
            return;
        }

        try
        {
            _ = await preparedRuntimeTask.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }

    public static void ObserveWhenReadyWithoutBlockingShutdown(
        Task<PreparedLoginEntryRuntime?>? preparedRuntimeTask)
    {
        if (preparedRuntimeTask is null)
        {
            return;
        }

        _ = ObserveWhenReadyAsync(preparedRuntimeTask);
    }
}

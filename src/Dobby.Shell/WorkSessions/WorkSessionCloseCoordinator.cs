using Dobby.Core;

namespace Dobby.Shell.WorkSessions;

public enum WorkSessionCloseReason
{
    Navigation,
    Logout,
    PluginDisable,
    PluginUninstall
}

public enum WorkSessionCloseChoice
{
    Cancel,
    Minimize,
    Close
}

public enum PendingWorkSessionAction
{
    Navigation,
    Logout,
    PluginDisable,
    PluginUninstall
}

public sealed record WorkSessionCloseRequest(
    Guid SessionId,
    WorkSessionCloseReason Reason,
    IReadOnlyList<WorkSessionCloseChoice> Choices);

public sealed record PendingWorkSessionCloseRequest(
    WorkSessionCloseRequest CloseRequest,
    PendingWorkSessionAction Action,
    string? RequestedRouteKey)
{
    public PendingWorkSessionCloseRequest(
        WorkSessionCloseRequest closeRequest,
        string requestedRouteKey)
        : this(closeRequest, PendingWorkSessionAction.Navigation, requestedRouteKey)
    {
    }
}

public sealed class WorkSessionCloseCoordinator(WorkSessionRegistry registry)
{
    public WorkSessionCloseRequest RequestClose(Guid sessionId, WorkSessionCloseReason reason)
    {
        return new WorkSessionCloseRequest(
            sessionId,
            reason,
            [
                WorkSessionCloseChoice.Cancel,
                WorkSessionCloseChoice.Minimize,
                WorkSessionCloseChoice.Close
            ]);
    }

    public PendingWorkSessionCloseRequest? RequestNavigation(string routeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);

        var session = FirstActiveSession();
        if (session is null)
        {
            return null;
        }

        return new PendingWorkSessionCloseRequest(
            RequestClose(session.SessionId, WorkSessionCloseReason.Navigation),
            routeKey);
    }

    public PendingWorkSessionCloseRequest? RequestLogout()
    {
        var session = FirstActiveSession();
        return session is null
            ? null
            : new PendingWorkSessionCloseRequest(
                RequestClose(session.SessionId, WorkSessionCloseReason.Logout),
                PendingWorkSessionAction.Logout,
                null);
    }

    public PendingWorkSessionCloseRequest? RequestPluginDisable(string pluginId)
    {
        return RequestPluginLifecycleAction(
            pluginId,
            WorkSessionCloseReason.PluginDisable,
            PendingWorkSessionAction.PluginDisable);
    }

    public PendingWorkSessionCloseRequest? RequestPluginUninstall(string pluginId)
    {
        return RequestPluginLifecycleAction(
            pluginId,
            WorkSessionCloseReason.PluginUninstall,
            PendingWorkSessionAction.PluginUninstall);
    }

    public Result ApplyChoice(WorkSessionCloseRequest request, WorkSessionCloseChoice choice)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Choices.Contains(choice))
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }

        switch (choice)
        {
            case WorkSessionCloseChoice.Cancel:
                return Result.Success();
            case WorkSessionCloseChoice.Minimize:
                registry.Minimize(request.SessionId);
                return Result.Success();
            case WorkSessionCloseChoice.Close:
                registry.Close(request.SessionId);
                return Result.Success();
            default:
                return Result.Failure(ErrorCode.UnsupportedOperation);
        }
    }

    public void HandleLock()
    {
    }

    public void HandleLogout()
    {
        foreach (var session in registry.Snapshot())
        {
            var request = RequestClose(session.SessionId, WorkSessionCloseReason.Logout);
            _ = ApplyChoice(request, WorkSessionCloseChoice.Close);
        }
    }

    private WorkSession? FirstActiveSession(string? ownerOrRouteKey = null)
    {
        return registry.Snapshot()
            .FirstOrDefault(candidate =>
                !candidate.IsMinimized &&
                (string.IsNullOrWhiteSpace(ownerOrRouteKey) ||
                    candidate.OwnerId.Equals(ownerOrRouteKey, StringComparison.Ordinal) ||
                    candidate.RouteKey.Equals(ownerOrRouteKey, StringComparison.Ordinal)));
    }

    private PendingWorkSessionCloseRequest? RequestPluginLifecycleAction(
        string pluginId,
        WorkSessionCloseReason reason,
        PendingWorkSessionAction action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var session = FirstActiveSession(pluginId);
        return session is null
            ? null
            : new PendingWorkSessionCloseRequest(
                RequestClose(session.SessionId, reason),
                action,
                null);
    }
}

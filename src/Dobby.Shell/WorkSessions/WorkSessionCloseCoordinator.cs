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

public sealed record WorkSessionCloseRequest
{
    public WorkSessionCloseRequest(
        Guid sessionId,
        WorkSessionCloseReason reason,
        IReadOnlyList<WorkSessionCloseChoice> choices)
        : this([sessionId], reason, choices)
    {
    }

    public WorkSessionCloseRequest(
        IReadOnlyList<Guid> sessionIds,
        WorkSessionCloseReason reason,
        IReadOnlyList<WorkSessionCloseChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        ArgumentNullException.ThrowIfNull(choices);

        if (sessionIds.Count == 0)
        {
            throw new ArgumentException("At least one work session is required.", nameof(sessionIds));
        }

        SessionIds = sessionIds.Distinct().ToArray();
        Reason = reason;
        Choices = choices.ToArray();
    }

    public IReadOnlyList<Guid> SessionIds { get; }

    public Guid SessionId => SessionIds[0];

    public WorkSessionCloseReason Reason { get; }

    public IReadOnlyList<WorkSessionCloseChoice> Choices { get; }
}

public sealed record PendingWorkSessionCloseRequest
{
    public PendingWorkSessionCloseRequest(
        WorkSessionCloseRequest closeRequest,
        string requestedRouteKey)
        : this(closeRequest, PendingWorkSessionAction.Navigation, requestedRouteKey, actionTargetId: null)
    {
    }

    public PendingWorkSessionCloseRequest(
        WorkSessionCloseRequest closeRequest,
        PendingWorkSessionAction action,
        string? requestedRouteKey,
        string? actionTargetId)
        : this(Guid.CreateVersion7(), closeRequest, action, requestedRouteKey, actionTargetId)
    {
    }

    public PendingWorkSessionCloseRequest(
        Guid requestId,
        WorkSessionCloseRequest closeRequest,
        PendingWorkSessionAction action,
        string? requestedRouteKey,
        string? actionTargetId)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A pending request ID is required.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(closeRequest);
        RequestId = requestId;
        CloseRequest = closeRequest;
        Action = action;
        RequestedRouteKey = requestedRouteKey;
        ActionTargetId = actionTargetId;
    }

    public Guid RequestId { get; }

    public WorkSessionCloseRequest CloseRequest { get; }

    public PendingWorkSessionAction Action { get; }

    public string? RequestedRouteKey { get; }

    public string? ActionTargetId { get; }
}

public sealed class WorkSessionCloseCoordinator(WorkSessionRegistry registry)
{
    public WorkSessionCloseRequest RequestClose(Guid sessionId, WorkSessionCloseReason reason)
    {
        return new WorkSessionCloseRequest(
            sessionId,
            reason,
            ChoicesFor(reason));
    }

    public PendingWorkSessionCloseRequest? RequestNavigation(
        string currentRouteKey,
        string requestedRouteKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentRouteKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedRouteKey);

        var sessionIds = registry.Snapshot()
            .Where(session =>
                !session.IsMinimized &&
                session.RouteKey.Equals(currentRouteKey, StringComparison.Ordinal))
            .Select(session => session.SessionId)
            .ToArray();
        return sessionIds.Length == 0
            ? null
            : new PendingWorkSessionCloseRequest(
                RequestClose(sessionIds, WorkSessionCloseReason.Navigation),
                requestedRouteKey);
    }

    public PendingWorkSessionCloseRequest? RequestLogout()
    {
        var sessionIds = registry.Snapshot()
            .Select(session => session.SessionId)
            .ToArray();
        return sessionIds.Length == 0
            ? null
            : new PendingWorkSessionCloseRequest(
                RequestClose(sessionIds, WorkSessionCloseReason.Logout),
                PendingWorkSessionAction.Logout,
                requestedRouteKey: null,
                actionTargetId: null);
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
                foreach (var sessionId in request.SessionIds)
                {
                    registry.Minimize(sessionId);
                }

                return Result.Success();
            case WorkSessionCloseChoice.Close:
                foreach (var sessionId in request.SessionIds)
                {
                    registry.Close(sessionId);
                }

                return Result.Success();
            default:
                return Result.Failure(ErrorCode.UnsupportedOperation);
        }
    }

    public IReadOnlyList<WorkSession> HandleLock()
    {
        return registry.Snapshot();
    }

    public PendingWorkSessionCloseRequest? HandleLogout()
    {
        return RequestLogout();
    }

    private PendingWorkSessionCloseRequest? RequestPluginLifecycleAction(
        string pluginId,
        WorkSessionCloseReason reason,
        PendingWorkSessionAction action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var sessionIds = registry.Snapshot()
            .Where(candidate => candidate.OwnerId.Equals(pluginId, StringComparison.Ordinal))
            .Select(candidate => candidate.SessionId)
            .ToArray();
        return sessionIds.Length == 0
            ? null
            : new PendingWorkSessionCloseRequest(
                RequestClose(sessionIds, reason),
                action,
                requestedRouteKey: null,
                actionTargetId: pluginId);
    }

    private static WorkSessionCloseRequest RequestClose(
        IReadOnlyList<Guid> sessionIds,
        WorkSessionCloseReason reason)
    {
        return new WorkSessionCloseRequest(sessionIds, reason, ChoicesFor(reason));
    }

    private static IReadOnlyList<WorkSessionCloseChoice> ChoicesFor(WorkSessionCloseReason reason)
    {
        return reason == WorkSessionCloseReason.Navigation
            ?
            [
                WorkSessionCloseChoice.Cancel,
                WorkSessionCloseChoice.Minimize,
                WorkSessionCloseChoice.Close
            ]
            :
            [
                WorkSessionCloseChoice.Cancel,
                WorkSessionCloseChoice.Close
            ];
    }
}

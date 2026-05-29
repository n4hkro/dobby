namespace Dobby.Shell.WorkSessions;

public sealed class WorkSessionRegistry
{
    private readonly Dictionary<Guid, WorkSession> _sessions = [];

    public WorkSession Open(string ownerId, string routeKey, string labelKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);

        var session = new WorkSession(
            Guid.CreateVersion7(),
            ownerId,
            routeKey,
            labelKey,
            false,
            WorkSessionProgress.Indeterminate(),
            null,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));
        _sessions.Add(session.SessionId, session);
        return session;
    }

    public void Minimize(Guid sessionId)
    {
        Update(sessionId, session => session with { IsMinimized = true });
    }

    public void Restore(Guid sessionId)
    {
        Update(sessionId, session => session with { IsMinimized = false });
    }

    public void UpdateProgress(Guid sessionId, WorkSessionProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        Update(sessionId, session => session with { Progress = progress });
    }

    public void RecordResult(Guid sessionId, string resultSummaryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultSummaryKey);
        Update(sessionId, session => session with { ResultSummaryKey = resultSummaryKey });
    }

    public void RecordError(Guid sessionId, string errorSummaryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorSummaryKey);
        Update(sessionId, session => session with { ErrorSummaryKey = errorSummaryKey });
    }

    public void RecordInputState(Guid sessionId, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        Update(sessionId, session => session with
        {
            InputState = WithValue(session.InputState, key, value)
        });
    }

    public void RecordScreenState(Guid sessionId, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        Update(sessionId, session => session with
        {
            ScreenState = WithValue(session.ScreenState, key, value)
        });
    }

    public void Close(Guid sessionId)
    {
        _sessions.Remove(sessionId);
    }

    public void Clear()
    {
        _sessions.Clear();
    }

    public WorkSession? Get(Guid sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    public IReadOnlyList<WorkSession> Snapshot()
    {
        return _sessions.Values.OrderBy(session => session.SessionId).ToArray();
    }

    private void Update(Guid sessionId, Func<WorkSession, WorkSession> update)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            _sessions[sessionId] = update(session);
        }
    }

    private static IReadOnlyDictionary<string, string> WithValue(
        IReadOnlyDictionary<string, string> source,
        string key,
        string value)
    {
        var copy = new Dictionary<string, string>(source, StringComparer.Ordinal)
        {
            [key] = value
        };
        return copy;
    }
}

public sealed record WorkSession(
    Guid SessionId,
    string OwnerId,
    string RouteKey,
    string LabelKey,
    bool IsMinimized,
    WorkSessionProgress Progress,
    string? ResultSummaryKey,
    string? ErrorSummaryKey,
    IReadOnlyDictionary<string, string> InputState,
    IReadOnlyDictionary<string, string> ScreenState);

public sealed record WorkSessionProgress(int? Percent, bool IsIndeterminate)
{
    public static WorkSessionProgress Indeterminate()
    {
        return new WorkSessionProgress(null, true);
    }

    public static WorkSessionProgress Determinate(int percent)
    {
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "Progress percent must be between 0 and 100.");
        }

        return new WorkSessionProgress(percent, false);
    }
}

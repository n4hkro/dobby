namespace Dobby.Shell.WorkSessions;

public sealed class WorkSessionRegistry
{
    /// <summary>Maximum concurrently open work sessions across all owners.</summary>
    public const int MaximumSessions = 64;

    /// <summary>Maximum concurrently open work sessions for a single owner (plugin or host area).</summary>
    public const int MaximumSessionsPerOwner = 16;

    /// <summary>Maximum length of any resource-key style argument (route, label, summary, state key).</summary>
    public const int MaximumKeyLength = 256;

    /// <summary>Maximum retained state entries per session, per bucket (input state, screen state).</summary>
    public const int MaximumStateEntries = 64;

    /// <summary>Maximum length of a retained state value.</summary>
    public const int MaximumStateValueLength = 4096;

    private readonly Dictionary<Guid, WorkSession> _sessions = [];

    public WorkSession Open(string ownerId, string routeKey, string labelKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);
        RequireBoundedKey(ownerId, nameof(ownerId));
        RequireBoundedKey(routeKey, nameof(routeKey));
        RequireBoundedKey(labelKey, nameof(labelKey));

        if (_sessions.Count >= MaximumSessions)
        {
            throw new InvalidOperationException(
                $"Work session capacity of {MaximumSessions} sessions is exhausted.");
        }

        var ownerCount = 0;
        foreach (var existing in _sessions.Values)
        {
            if (existing.OwnerId.Equals(ownerId, StringComparison.Ordinal))
            {
                ownerCount++;
            }
        }

        if (ownerCount >= MaximumSessionsPerOwner)
        {
            throw new InvalidOperationException(
                $"Owner work session capacity of {MaximumSessionsPerOwner} sessions is exhausted.");
        }

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
        RequireBoundedKey(resultSummaryKey, nameof(resultSummaryKey));
        Update(sessionId, session => session with { ResultSummaryKey = resultSummaryKey });
    }

    public void RecordError(Guid sessionId, string errorSummaryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorSummaryKey);
        RequireBoundedKey(errorSummaryKey, nameof(errorSummaryKey));
        Update(sessionId, session => session with { ErrorSummaryKey = errorSummaryKey });
    }

    public void RecordInputState(Guid sessionId, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        RequireBoundedKey(key, nameof(key));
        RequireBoundedStateValue(value);

        Update(sessionId, session => session with
        {
            InputState = WithValue(session.InputState, key, value)
        });
    }

    public void RecordScreenState(Guid sessionId, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        RequireBoundedKey(key, nameof(key));
        RequireBoundedStateValue(value);

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

    private static void RequireBoundedKey(string value, string parameterName)
    {
        if (value.Length > MaximumKeyLength)
        {
            throw new ArgumentException(
                $"Value exceeds the maximum key length of {MaximumKeyLength} characters.",
                parameterName);
        }
    }

    private static void RequireBoundedStateValue(string value)
    {
        if (value.Length > MaximumStateValueLength)
        {
            throw new ArgumentException(
                $"State value exceeds the maximum length of {MaximumStateValueLength} characters.",
                nameof(value));
        }
    }

    private static IReadOnlyDictionary<string, string> WithValue(
        IReadOnlyDictionary<string, string> source,
        string key,
        string value)
    {
        if (!source.ContainsKey(key) && source.Count >= MaximumStateEntries)
        {
            throw new InvalidOperationException(
                $"Work session state capacity of {MaximumStateEntries} entries is exhausted.");
        }

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

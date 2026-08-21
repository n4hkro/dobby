using Dobby.Core;

namespace Dobby.Security.Authentication;

public sealed class ActiveSessionAccessor
{
    private readonly object gate = new();
    private SessionRecord? current;

    public SessionRecord? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public Result SetActive(SessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (gate)
        {
            if (current is not null)
            {
                return Result.Failure(ErrorCode.AlreadyExists);
            }

            current = session;
            return Result.Success();
        }
    }

    public void Replace(SessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (gate)
        {
            current = session;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            current = null;
        }
    }
}

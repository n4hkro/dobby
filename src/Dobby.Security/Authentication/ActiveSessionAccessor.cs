using Dobby.Core;

namespace Dobby.Security.Authentication;

public sealed class ActiveSessionAccessor
{
    public SessionRecord? Current { get; private set; }

    public Result SetActive(SessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (Current is not null)
        {
            return Result.Failure(ErrorCode.AlreadyExists);
        }

        Current = session;
        return Result.Success();
    }

    public void Replace(SessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Current = session;
    }

    public void Clear()
    {
        Current = null;
    }
}

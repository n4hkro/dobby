using Dobby.Core;

namespace Dobby.Configuration;

public sealed record SecuritySessionPreference(bool AutoLockEnabled, int AutoLockMinutes)
{
    public static Result<SecuritySessionPreference> Create(bool autoLockEnabled, int autoLockMinutes)
    {
        return autoLockMinutes <= 0
            ? Result.Failure<SecuritySessionPreference>(ErrorCode.InvalidInput)
            : Result.Success(new SecuritySessionPreference(autoLockEnabled, autoLockMinutes));
    }
}

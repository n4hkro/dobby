using Dobby.Configuration;
using Dobby.Core;

namespace Dobby.Host.Settings;

public sealed class SecuritySessionSettingsModel(ISettingsPreferenceSource preferenceSource)
{
    public bool AutoLockEnabled => preferenceSource.Load().Value.SecuritySession.AutoLockEnabled;

    public int AutoLockMinutes => preferenceSource.Load().Value.SecuritySession.AutoLockMinutes;

    public Result SetAutoLock(bool autoLockEnabled, int autoLockMinutes)
    {
        var validation = SecuritySessionPreference.Create(autoLockEnabled, autoLockMinutes);
        if (!validation.IsSuccess)
        {
            return Result.Failure(validation.ErrorCode!.Value);
        }

        var preferences = preferenceSource.Load();
        if (!preferences.IsSuccess)
        {
            return Result.Failure(preferences.ErrorCode!.Value);
        }

        return preferenceSource.Save(preferences.Value with
        {
            SecuritySession = validation.Value
        });
    }
}

using Dobby.Core;
using Dobby.Host.Login;

namespace Dobby.Host.Settings;

public sealed class AccountSettingsModel(
    Func<string, string, string, bool, CorrelationId, Result> passwordChangeShortcut)
{
    public bool HasPasswordChangeShortcut => true;

    public bool HasManualLockShortcut => false;

    public bool HasLogoutShortcut => false;

    public Result ChangePassword(
        string userId,
        string sessionId,
        string newPassword,
        bool rememberMe,
        CorrelationId correlationId)
    {
        return passwordChangeShortcut(userId, sessionId, newPassword, rememberMe, correlationId);
    }

}

using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Localization;

namespace Dobby.Host.Settings;

public sealed class AccountSettingsModel(
    Func<string, string, string, string, string, bool, CorrelationId, Result> passwordChangeShortcut)
{
    public bool HasPasswordChangeShortcut => true;

    public bool HasManualLockShortcut => false;

    public bool HasLogoutShortcut => false;

    public Result ChangePassword(
        string userId,
        string sessionId,
        string currentPassword,
        string newPassword,
        string newPasswordConfirmation,
        bool rememberMe,
        CorrelationId correlationId)
    {
        return passwordChangeShortcut(
            userId,
            sessionId,
            currentPassword,
            newPassword,
            newPasswordConfirmation,
            rememberMe,
            correlationId);
    }

    public AccountSettingsSection BuildSection()
    {
        return new AccountSettingsSection(
            HostResourceKeys.ShellProfileAccountInfo,
            HostResourceKeys.ShellProfileChangePassword,
            HasPasswordChangeShortcut,
            HasManualLockShortcut,
            HasLogoutShortcut);
    }
}

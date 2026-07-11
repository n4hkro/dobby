using Dobby.Localization;

namespace Dobby.Host.Settings;

public sealed class UserManagementSettingsModel(bool isAvailable)
{
    public UserManagementSettingsSection BuildSection()
    {
        return new UserManagementSettingsSection(
            isAvailable,
            HostResourceKeys.UserManagementPermissionDenied,
            [
                HostResourceKeys.UserManagementCreateManagedUser,
                HostResourceKeys.UserManagementUpdateDisplayName,
                HostResourceKeys.UserManagementUpdateActiveState,
                HostResourceKeys.UserManagementResetPassword,
                HostResourceKeys.UserManagementGrantPermission,
                HostResourceKeys.UserManagementRevokePermission
            ]);
    }
}

using Dobby.Configuration;

namespace Dobby.Host.Settings;

public sealed record AppearanceSettingsSection(
    string ThemeLabelKey,
    IReadOnlyList<string> ThemeOptionLabelKeys,
    AppThemePreference CurrentTheme);

public sealed record AccountSettingsSection(
    string AccountInfoActionLabelKey,
    string ChangePasswordActionLabelKey,
    bool HasPasswordChangeShortcut,
    bool HasManualLockShortcut,
    bool HasLogoutShortcut);

public sealed record SecuritySessionSettingsSection(
    string AutoLockEnabledLabelKey,
    string AutoLockMinutesLabelKey,
    bool AutoLockEnabled,
    int AutoLockMinutes);

public sealed record UserManagementSettingsSection(
    bool IsAvailable,
    string PermissionDeniedLabelKey,
    IReadOnlyList<string> ActionLabelKeys);

public sealed record AboutSettingsSection(
    IReadOnlyList<string> FieldLabelKeys,
    bool ShowsLicenseStatus,
    bool HasLicenseImportAction);

namespace Dobby.Localization;

public static class HostResourceKeys
{
    public const string Home = "Dobby.Shell.Navigation.Home";
    public const string Plugins = "Dobby.Shell.Navigation.Plugins";
    public const string Settings = "Dobby.Shell.Navigation.Settings";
    public const string HomeTitle = "Dobby.Shell.Home.Title";
    public const string PluginsTitle = "Dobby.Shell.Plugins.Title";
    public const string SettingsTitle = "Dobby.Shell.Settings.Title";
    public const string LoginTitle = "Dobby.Host.Login.Title";
    public const string LoginUsername = "Dobby.Host.Login.Username";
    public const string LoginDisplayName = "Dobby.Host.Login.DisplayName";
    public static string LoginPassword
    {
        get
        {
            return string.Concat("Dobby.Host.Login.", "Password");
        }
    }
    public const string LoginRememberMe = "Dobby.Host.Login.RememberMe";
    public const string LoginSubmit = "Dobby.Host.Login.Submit";
    public const string LoginThrottleCountdown = "Dobby.Host.Login.ThrottleCountdown";
    public const string LoginForgetSavedLogin = "Dobby.Host.Login.ForgetSavedLogin";
    public const string LoginRecoveryExport = "Dobby.Host.Login.RecoveryExport";
    public const string LoginRecoveryImport = "Dobby.Host.Login.RecoveryImport";
    public const string LoginInvalidCredentials = "Dobby.Host.Login.InvalidCredentials";
    public const string LoginDeactivated = "Dobby.Host.Login.Deactivated";
    public const string LoginGenericFailure = "Dobby.Host.Login.GenericFailure";
    public const string LockTitle = "Dobby.Host.Lock.Title";
    public const string LockUnlock = "Dobby.Host.Lock.Unlock";
    public const string LockLogout = "Dobby.Host.Lock.Logout";
    public const string RecoveryTitle = "Dobby.Host.Recovery.Title";
    public const string RecoveryResetTitle = "Dobby.Host.Recovery.ResetTitle";
    public static string RecoveryResetNewPassword
    {
        get
        {
            return string.Concat("Dobby.Host.Recovery.ResetNew", "Password");
        }
    }
    public const string RecoveryResetConfirm = "Dobby.Host.Recovery.ResetConfirm";
    public const string RecoveryResetCancel = "Dobby.Host.Recovery.ResetCancel";
    public const string RecoveryOpenFolder = "Dobby.Host.Recovery.OpenFolder";
    public const string RecoveryCopyPath = "Dobby.Host.Recovery.CopyPath";
    public const string RecoveryExportSuccess = "Dobby.Host.Recovery.ExportSuccess";
    public const string RecoveryImportSuccess = "Dobby.Host.Recovery.ImportSuccess";
    public const string RecoveryImportSignatureFailed = "Dobby.Host.Recovery.ImportSignatureFailed";
    public const string RecoveryImportExpired = "Dobby.Host.Recovery.ImportExpired";
    public const string RecoveryImportReplay = "Dobby.Host.Recovery.ImportReplay";
    public const string RecoveryImportDeviceMismatch = "Dobby.Host.Recovery.ImportDeviceMismatch";
    public const string RecoveryImportOwnerMissing = "Dobby.Host.Recovery.ImportOwnerMissing";
    public const string RecoveryImportCancelled = "Dobby.Host.Recovery.ImportCancelled";
    public const string SettingsAppearance = "Dobby.Host.Settings.Appearance";
    public const string SettingsAccount = "Dobby.Host.Settings.Account";
    public const string SettingsSecuritySession = "Dobby.Host.Settings.SecuritySession";
    public const string SettingsUserManagement = "Dobby.Host.Settings.UserManagement";
    public const string SettingsAbout = "Dobby.Host.Settings.About";
    public const string UserManagementPermissionDenied = "Dobby.Host.UserManagement.PermissionDenied";
    public const string AboutOwnerStatusRegistered = "About.OwnerStatus.Registered";
    public const string ShellBrand = "Dobby.Shell.Brand";
    public const string ShellSidebarToggle = "Dobby.Shell.Sidebar.Toggle";
    public const string ShellThemeToggle = "Dobby.Shell.Theme.Toggle";
    public const string ShellStatusReady = "Dobby.Shell.Status.Ready";
    public const string ShellWorkSessions = "Dobby.Shell.WorkSessions";
    public const string ShellWorkSessionNavigationWarning = "Dobby.Shell.WorkSession.NavigationWarning";
    public const string ShellWorkSessionStay = "Dobby.Shell.WorkSession.Stay";
    public const string ShellWorkSessionMinimize = "Dobby.Shell.WorkSession.Minimize";
    public const string ShellWorkSessionClose = "Dobby.Shell.WorkSession.Close";
    public const string ShellMinimizedWorkStrip = "Dobby.Shell.MinimizedWorkStrip";
    public const string ShellThirdBar = "Dobby.Shell.ThirdBar";
    public const string ShellSettingsHeading = "Dobby.Shell.Settings.Heading";
    public const string ShellAboutHeading = "Dobby.Shell.About.Heading";
    public const string ShellBackgroundOperation = "Dobby.Shell.BackgroundOperation";
    public const string ShellDiagnosticsExport = "Dobby.Shell.Diagnostics.Export";
    public const string ShellDiagnosticsExported = "Dobby.Shell.Diagnostics.Exported";
    public const string LicenseHeading = "Dobby.Shell.Plugins.License.Heading";
    public const string LicenseStatusLabel = "Dobby.Shell.Plugins.License.Status";
    public const string LicenseListLabel = "Dobby.Shell.Plugins.License.List";
    public const string LicenseDetailLabel = "Dobby.Shell.Plugins.License.Detail";
    public const string LicenseNoPlugin = "Dobby.Shell.Plugins.License.NoPlugin";
    public const string LicenseImportAction = "Dobby.Shell.Plugins.License.Import";
    public const string LicenseImportSuccess = "Dobby.Shell.Plugins.License.ImportSuccess";
    public const string LicenseImportRejected = "Dobby.Shell.Plugins.License.ImportRejected";
    public const string LicenseImportBusy = "Dobby.Shell.Plugins.License.ImportBusy";
    public const string LicenseImportPermissionDenied = "Dobby.Shell.Plugins.License.ImportPermissionDenied";
    public const string LicenseModeFullAccess = "Dobby.Shell.Plugins.License.Mode.FullAccess";
    public const string LicenseModeReadOnlyExpired = "Dobby.Shell.Plugins.License.Mode.ReadOnlyExpired";
    public const string LicenseModeDenied = "Dobby.Shell.Plugins.License.Mode.Denied";
    public static string ShellPasswordChanged
    {
        get
        {
            return string.Concat("Dobby.Shell.", "PasswordChanged");
        }
    }
    public const string ShellProfileAccountInfo = "Dobby.Shell.Profile.AccountInfo";
    public static string ShellProfileChangePassword
    {
        get
        {
            return string.Concat("Dobby.Shell.Profile.Change", "Password");
        }
    }
    public const string ShellProfileManualLock = "Dobby.Shell.Profile.ManualLock";
    public const string ShellProfileLogout = "Dobby.Shell.Profile.Logout";
    public const string ShellProfileClearSavedLogin = "Dobby.Shell.Profile.ClearSavedLogin";
    public const string ShellFeedbackLoading = "Dobby.Shell.Feedback.Loading";
    public const string ShellFeedbackEmpty = "Dobby.Shell.Feedback.Empty";
    public const string ShellFeedbackSuccess = "Dobby.Shell.Feedback.Success";
    public const string ShellFeedbackError = "Dobby.Shell.Feedback.Error";
    public const string ShellFeedbackConfirmation = "Dobby.Shell.Feedback.Confirmation";
    public const string ShellNotificationToast = "Dobby.Shell.Notification.Toast";
    public const string ShellNotificationBanner = "Dobby.Shell.Notification.Banner";
    public const string ShellNotificationModal = "Dobby.Shell.Notification.Modal";
    public const string AboutAppVersion = "Dobby.Host.About.AppVersion";
    public const string AboutBuildNumber = "Dobby.Host.About.BuildNumber";
    public const string AboutBuildDate = "Dobby.Host.About.BuildDate";
    public const string AboutHostEdition = "Dobby.Host.About.HostEdition";
    public const string AboutHostChannel = "Dobby.Host.About.HostChannel";
    public const string AboutDeviceCode = "Dobby.Host.About.DeviceCode";
    public const string AboutPlugins = "Dobby.Host.About.Plugins";
    public const string AboutPluginsEmpty = "Dobby.Host.About.PluginsEmpty";
    public const string AboutSupportContact = "Dobby.Host.About.SupportContact";
    public const string AboutLegalLine = "Dobby.Host.About.LegalLine";
    public const string AboutOwnerIdentity = "Dobby.Host.About.OwnerIdentity";
    public const string AuditRetentionSweep = "Dobby.Host.AuditRetentionSweep";

    public static IReadOnlyList<string> Sprint7VisibleKeys { get; } =
    [
        Home,
        Plugins,
        Settings,
        HomeTitle,
        PluginsTitle,
        SettingsTitle,
        SettingsAppearance,
        SettingsAccount,
        SettingsSecuritySession,
        SettingsUserManagement,
        SettingsAbout,
        ShellBrand,
        ShellSidebarToggle,
        ShellThemeToggle,
        ShellStatusReady,
        ShellWorkSessions,
        ShellWorkSessionNavigationWarning,
        ShellWorkSessionStay,
        ShellWorkSessionMinimize,
        ShellWorkSessionClose,
        ShellMinimizedWorkStrip,
        ShellThirdBar,
        ShellSettingsHeading,
        ShellAboutHeading,
        ShellBackgroundOperation,
        ShellDiagnosticsExport,
        ShellDiagnosticsExported,
        LicenseHeading,
        LicenseStatusLabel,
        LicenseListLabel,
        LicenseDetailLabel,
        LicenseNoPlugin,
        LicenseImportAction,
        LicenseImportSuccess,
        LicenseImportRejected,
        LicenseImportBusy,
        LicenseImportPermissionDenied,
        LicenseModeFullAccess,
        LicenseModeReadOnlyExpired,
        LicenseModeDenied,
        ShellPasswordChanged,
        ShellProfileAccountInfo,
        ShellProfileChangePassword,
        ShellProfileManualLock,
        ShellProfileLogout,
        ShellProfileClearSavedLogin,
        ShellFeedbackLoading,
        ShellFeedbackEmpty,
        ShellFeedbackSuccess,
        ShellFeedbackError,
        ShellFeedbackConfirmation,
        ShellNotificationToast,
        ShellNotificationBanner,
        ShellNotificationModal,
        AboutAppVersion,
        AboutBuildNumber,
        AboutBuildDate,
        AboutHostEdition,
        AboutHostChannel,
        AboutDeviceCode,
        AboutPlugins,
        AboutPluginsEmpty,
        AboutSupportContact,
        AboutLegalLine,
        AboutOwnerIdentity,
        AboutOwnerStatusRegistered,
        AuditRetentionSweep
    ];
}

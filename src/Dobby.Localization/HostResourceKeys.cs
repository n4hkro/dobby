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
    public const string RecoveryResetValidation = "Dobby.Host.Recovery.ResetValidation";
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
    public const string SettingsAppearanceTheme = "Dobby.Host.Settings.Appearance.Theme";
    public const string SettingsAppearanceThemeLight = "Dobby.Host.Settings.Appearance.Theme.Light";
    public const string SettingsAppearanceThemeDark = "Dobby.Host.Settings.Appearance.Theme.Dark";
    public const string SettingsAccount = "Dobby.Host.Settings.Account";
    public const string SettingsSecuritySession = "Dobby.Host.Settings.SecuritySession";
    public const string SettingsSecuritySessionAutoLockEnabled = "Dobby.Host.Settings.SecuritySession.AutoLockEnabled";
    public const string SettingsSecuritySessionAutoLockMinutes = "Dobby.Host.Settings.SecuritySession.AutoLockMinutes";
    public const string SettingsSecuritySessionSave = "Dobby.Host.Settings.SecuritySession.Save";
    public const string SettingsUserManagement = "Dobby.Host.Settings.UserManagement";
    public const string SettingsAbout = "Dobby.Host.Settings.About";
    public const string UserManagementPermissionDenied = "Dobby.Host.UserManagement.PermissionDenied";
    public const string UserManagementCreateManagedUser = "Dobby.Host.UserManagement.CreateManagedUser";
    public const string UserManagementUpdateDisplayName = "Dobby.Host.UserManagement.UpdateDisplayName";
    public const string UserManagementUpdateActiveState = "Dobby.Host.UserManagement.UpdateActiveState";
    public const string UserManagementResetPassword = "Dobby.Host.UserManagement.ResetPassword";
    public const string UserManagementGrantPermission = "Dobby.Host.UserManagement.GrantPermission";
    public const string UserManagementRevokePermission = "Dobby.Host.UserManagement.RevokePermission";
    public const string AboutOwnerStatusManaged = "About.OwnerStatus.Managed";
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
    public const string ShellWorkSessionRestore = "Dobby.Shell.WorkSession.Restore";
    public const string ShellMinimizedWorkStrip = "Dobby.Shell.MinimizedWorkStrip";
    public const string ShellThirdBar = "Dobby.Shell.ThirdBar";
    public const string ShellWorkspaceNewTab = "Dobby.Shell.Workspace.NewTab";
    public const string ShellWorkspaceCloseTab = "Dobby.Shell.Workspace.CloseTab";
    public const string ShellWindowMinimize = "Dobby.Shell.Window.Minimize";
    public const string ShellWindowMaximize = "Dobby.Shell.Window.Maximize";
    public const string ShellWindowRestore = "Dobby.Shell.Window.Restore";
    public const string ShellWindowClose = "Dobby.Shell.Window.Close";
    public const string ShellWindowCloseConfirmationTitle = "Dobby.Shell.Window.CloseConfirmation.Title";
    public const string ShellWindowCloseConfirmationMessage = "Dobby.Shell.Window.CloseConfirmation.Message";
    public const string ShellSettingsHeading = "Dobby.Shell.Settings.Heading";
    public const string ShellAboutHeading = "Dobby.Shell.About.Heading";
    public const string ShellBackgroundOperation = "Dobby.Shell.BackgroundOperation";
    public const string ShellDashboardCasesOverview = "Dobby.Shell.Dashboard.CasesOverview";
    public const string ShellDashboardPipelines = "Dobby.Shell.Dashboard.Pipelines";
    public const string ShellDashboardInProgress = "Dobby.Shell.Dashboard.InProgress";
    public const string ShellDashboardCompleted = "Dobby.Shell.Dashboard.Completed";
    public const string ShellDashboardExpired = "Dobby.Shell.Dashboard.Expired";
    public const string ShellDashboardNewCases = "Dobby.Shell.Dashboard.NewCases";
    public const string ShellDashboardPendingInfo = "Dobby.Shell.Dashboard.PendingInfo";
    public const string ShellDashboardClosed = "Dobby.Shell.Dashboard.Closed";
    public const string ShellDashboardTopDrivers = "Dobby.Shell.Dashboard.TopDrivers";
    public const string ShellDashboardCaseMetrics = "Dobby.Shell.Dashboard.CaseMetrics";
    public const string ShellDiagnosticsExport = "Dobby.Shell.Diagnostics.Export";
    public const string ShellDiagnosticsExported = "Dobby.Shell.Diagnostics.Exported";
    public const string ShellDiagnosticsExportBusy = "Dobby.Shell.Diagnostics.Export.Busy";
    public const string ShellDiagnosticsExportDenied = "Dobby.Shell.Diagnostics.Export.Denied";
    public const string ShellDiagnosticsExportError = "Dobby.Shell.Diagnostics.Export.Error";
    public const string ShellDiagnosticsExportSuccess = "Dobby.Shell.Diagnostics.Export.Success";
    public const string PluginsManagerHeading = "Dobby.Shell.Plugins.Manager.Heading";
    public const string PluginsManagerInstall = "Dobby.Shell.Plugins.Manager.Install";
    public const string PluginsManagerEnable = "Dobby.Shell.Plugins.Manager.Enable";
    public const string PluginsManagerDisable = "Dobby.Shell.Plugins.Manager.Disable";
    public const string PluginsManagerUninstall = "Dobby.Shell.Plugins.Manager.Uninstall";
    public const string PluginsManagerEmpty = "Dobby.Shell.Plugins.Manager.Empty";
    public const string PluginPageUnavailable = "Dobby.Shell.Plugins.Page.Unavailable";
    public const string PluginsManagerRestartRequired = "Dobby.Shell.Plugins.Manager.RestartRequired";
    public const string PluginsManagerInstallBusy = "Dobby.Shell.Plugins.Manager.InstallBusy";
    public const string PluginsManagerEnableBusy = "Dobby.Shell.Plugins.Manager.EnableBusy";
    public const string PluginsManagerDisableBusy = "Dobby.Shell.Plugins.Manager.DisableBusy";
    public const string PluginsManagerUninstallBusy = "Dobby.Shell.Plugins.Manager.UninstallBusy";
    public const string PluginsManagerOperationDenied = "Dobby.Shell.Plugins.Manager.OperationDenied";
    public const string PluginsManagerReadOnlyDenied = "Dobby.Shell.Plugins.Manager.ReadOnlyDenied";
    public const string PluginsManagerDetailDisplayName = "Dobby.Shell.Plugins.Manager.Detail.DisplayName";
    public const string PluginsManagerDetailPluginId = "Dobby.Shell.Plugins.Manager.Detail.PluginId";
    public const string PluginsManagerDetailVersion = "Dobby.Shell.Plugins.Manager.Detail.Version";
    public const string PluginsManagerDetailStatus = "Dobby.Shell.Plugins.Manager.Detail.Status";
    public const string PluginsManagerDetailTrust = "Dobby.Shell.Plugins.Manager.Detail.Trust";
    public const string PluginsManagerDetailLicense = "Dobby.Shell.Plugins.Manager.Detail.License";
    public const string PluginsManagerDetailFault = "Dobby.Shell.Plugins.Manager.Detail.Fault";
    public const string PluginsManagerDetailRejection = "Dobby.Shell.Plugins.Manager.Detail.Rejection";
    public const string PluginsManagerStatusEnabled = "Dobby.Shell.Plugins.Manager.Status.Enabled";
    public const string PluginsManagerStatusDisabled = "Dobby.Shell.Plugins.Manager.Status.Disabled";
    public const string PluginsManagerStatusPendingUninstall = "Dobby.Shell.Plugins.Manager.Status.PendingUninstall";
    public const string PluginsManagerStatusFaulted = "Dobby.Shell.Plugins.Manager.Status.Faulted";
    public const string PluginsManagerStatusRejected = "Dobby.Shell.Plugins.Manager.Status.Rejected";
    public const string PluginsManagerStatusAvailable = "Dobby.Shell.Plugins.Manager.Status.Available";
    public const string PluginsManagerTrustTrusted = "Dobby.Shell.Plugins.Manager.Trust.Trusted";
    public const string PluginsManagerTrustRejected = "Dobby.Shell.Plugins.Manager.Trust.Rejected";
    public const string PluginsManagerTrustUnavailable = "Dobby.Shell.Plugins.Manager.Trust.Unavailable";
    public const string PluginsManagerLicenseFullAccess = "Dobby.Shell.Plugins.Manager.License.FullAccess";
    public const string PluginsManagerLicenseReadOnly = "Dobby.Shell.Plugins.Manager.License.ReadOnly";
    public const string PluginsManagerLicenseDenied = "Dobby.Shell.Plugins.Manager.License.Denied";
    public const string PluginsManagerLicenseUnavailable = "Dobby.Shell.Plugins.Manager.License.Unavailable";
    public const string PluginsManagerFaultNone = "Dobby.Shell.Plugins.Manager.Fault.None";
    public const string PluginsManagerFaultDetected = "Dobby.Shell.Plugins.Manager.Fault.Detected";
    public const string PluginsManagerRejectionSafe = "Dobby.Shell.Plugins.Manager.Rejection.Safe";
    public const string PluginsManagerUninstallConfirmationTitle = "Dobby.Shell.Plugins.Manager.UninstallConfirmation.Title";
    public const string PluginsManagerUninstallConfirmationMessage = "Dobby.Shell.Plugins.Manager.UninstallConfirmation.Message";
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
    public const string PrintReadOnlyConfirmationTitle = "Dobby.Shell.Print.ReadOnlyConfirmation.Title";
    public const string PrintReadOnlyConfirmationMessage = "Dobby.Shell.Print.ReadOnlyConfirmation.Message";
    public static string ShellPasswordChanged
    {
        get
        {
            return string.Concat("Dobby.Shell.", "PasswordChanged");
        }
    }
    public const string ShellProfileAccountInfo = "Dobby.Shell.Profile.AccountInfo";
    public const string ShellProfileAnonymous = "Dobby.Shell.Profile.Anonymous";
    public const string ShellProfileChangePassword = "Dobby.Shell.Profile.ChangePassword";
    public const string ShellProfileCurrentPassword = "Dobby.Shell.Profile.PasswordChange.CurrentPassword";
    public const string ShellProfileNewPassword = "Dobby.Shell.Profile.PasswordChange.NewPassword";
    public const string ShellProfileConfirmNewPassword = "Dobby.Shell.Profile.PasswordChange.ConfirmNewPassword";
    public const string ShellProfilePasswordChangeBusy = "Dobby.Shell.Profile.PasswordChange.Busy";
    public const string ShellProfilePasswordChangeDenied = "Dobby.Shell.Profile.PasswordChange.Denied";
    public const string ShellProfilePasswordChangeError = "Dobby.Shell.Profile.PasswordChange.Error";
    public const string ShellProfilePasswordChangeSuccess = "Dobby.Shell.Profile.PasswordChange.Success";
    public const string ShellProfileManualLock = "Dobby.Shell.Profile.ManualLock";
    public const string ShellProfileManualLockBusy = "Dobby.Shell.Profile.ManualLock.Busy";
    public const string ShellProfileManualLockDenied = "Dobby.Shell.Profile.ManualLock.Denied";
    public const string ShellProfileManualLockError = "Dobby.Shell.Profile.ManualLock.Error";
    public const string ShellProfileManualLockSuccess = "Dobby.Shell.Profile.ManualLock.Success";
    public const string ShellProfileLogout = "Dobby.Shell.Profile.Logout";
    public const string ShellProfileLogoutBusy = "Dobby.Shell.Profile.Logout.Busy";
    public const string ShellProfileLogoutDenied = "Dobby.Shell.Profile.Logout.Denied";
    public const string ShellProfileLogoutError = "Dobby.Shell.Profile.Logout.Error";
    public const string ShellProfileLogoutSuccess = "Dobby.Shell.Profile.Logout.Success";
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
    public const string AboutArtifactMode = "Dobby.Host.About.ArtifactMode";
    public const string AboutArtifactModeInstalled = "Dobby.Host.About.ArtifactMode.Installed";
    public const string AboutArtifactModePortableSupport = "Dobby.Host.About.ArtifactMode.PortableSupport";
    public const string AboutDeviceCode = "Dobby.Host.About.DeviceCode";
    public const string AboutDeviceCodeExplanation = "Dobby.Host.About.DeviceCode.Explanation";
    public const string AboutDeviceCodeCopy = "Dobby.Host.About.DeviceCode.Copy";
    public const string AboutDeviceCodeCopied = "Dobby.Host.About.DeviceCode.Copied";
    public const string AboutDeviceCodeCopyError = "Dobby.Host.About.DeviceCode.CopyError";
    public const string AboutPlugins = "Dobby.Host.About.Plugins";
    public const string AboutPluginsEmpty = "Dobby.Host.About.PluginsEmpty";
    public const string AboutSupportContact = "Dobby.Host.About.SupportContact";
    public const string AboutLegalLine = "Dobby.Host.About.LegalLine";
    public const string AboutOwnerIdentity = "Dobby.Host.About.OwnerIdentity";

    public const string ShellCancelOperation = "Dobby.Shell.Operation.Cancel";
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
        SettingsAppearanceTheme,
        SettingsAppearanceThemeLight,
        SettingsAppearanceThemeDark,
        SettingsAccount,
        SettingsSecuritySession,
        SettingsSecuritySessionAutoLockEnabled,
        SettingsSecuritySessionAutoLockMinutes,
        SettingsSecuritySessionSave,
        SettingsUserManagement,
        SettingsAbout,
        UserManagementPermissionDenied,
        UserManagementCreateManagedUser,
        UserManagementUpdateDisplayName,
        UserManagementUpdateActiveState,
        UserManagementResetPassword,
        UserManagementGrantPermission,
        UserManagementRevokePermission,
        AboutOwnerStatusManaged,
        ShellBrand,
        ShellSidebarToggle,
        ShellThemeToggle,
        ShellStatusReady,
        ShellWorkSessions,
        ShellWorkSessionNavigationWarning,
        ShellWorkSessionStay,
        ShellWorkSessionMinimize,
        ShellWorkSessionClose,
        ShellWorkSessionRestore,
        ShellMinimizedWorkStrip,
        ShellThirdBar,
        ShellWorkspaceNewTab,
        ShellWorkspaceCloseTab,
        ShellWindowMinimize,
        ShellWindowMaximize,
        ShellWindowRestore,
        ShellWindowClose,
        ShellWindowCloseConfirmationTitle,
        ShellWindowCloseConfirmationMessage,
        ShellSettingsHeading,
        ShellAboutHeading,
        ShellBackgroundOperation,
        ShellDashboardCasesOverview,
        ShellDashboardPipelines,
        ShellDashboardInProgress,
        ShellDashboardCompleted,
        ShellDashboardExpired,
        ShellDashboardNewCases,
        ShellDashboardPendingInfo,
        ShellDashboardClosed,
        ShellDashboardTopDrivers,
        ShellDashboardCaseMetrics,
        ShellDiagnosticsExport,
        ShellDiagnosticsExported,
        ShellDiagnosticsExportBusy,
        ShellDiagnosticsExportDenied,
        ShellDiagnosticsExportError,
        ShellDiagnosticsExportSuccess,
        PluginsManagerHeading,
        PluginsManagerInstall,
        PluginsManagerEnable,
        PluginsManagerDisable,
        PluginsManagerUninstall,
        PluginsManagerEmpty,
        PluginPageUnavailable,
        PluginsManagerRestartRequired,
        PluginsManagerInstallBusy,
        PluginsManagerEnableBusy,
        PluginsManagerDisableBusy,
        PluginsManagerUninstallBusy,
        PluginsManagerOperationDenied,
        PluginsManagerReadOnlyDenied,
        PluginsManagerDetailDisplayName,
        PluginsManagerDetailPluginId,
        PluginsManagerDetailVersion,
        PluginsManagerDetailStatus,
        PluginsManagerDetailTrust,
        PluginsManagerDetailLicense,
        PluginsManagerDetailFault,
        PluginsManagerDetailRejection,
        PluginsManagerStatusEnabled,
        PluginsManagerStatusDisabled,
        PluginsManagerStatusPendingUninstall,
        PluginsManagerStatusFaulted,
        PluginsManagerStatusRejected,
        PluginsManagerStatusAvailable,
        PluginsManagerTrustTrusted,
        PluginsManagerTrustRejected,
        PluginsManagerTrustUnavailable,
        PluginsManagerLicenseFullAccess,
        PluginsManagerLicenseReadOnly,
        PluginsManagerLicenseDenied,
        PluginsManagerLicenseUnavailable,
        PluginsManagerFaultNone,
        PluginsManagerFaultDetected,
        PluginsManagerRejectionSafe,
        PluginsManagerUninstallConfirmationTitle,
        PluginsManagerUninstallConfirmationMessage,
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
        PrintReadOnlyConfirmationTitle,
        PrintReadOnlyConfirmationMessage,
        ShellPasswordChanged,
        ShellProfileAccountInfo,
        ShellProfileAnonymous,
        ShellProfileChangePassword,
        ShellProfileCurrentPassword,
        ShellProfileNewPassword,
        ShellProfileConfirmNewPassword,
        ShellProfilePasswordChangeBusy,
        ShellProfilePasswordChangeDenied,
        ShellProfilePasswordChangeError,
        ShellProfilePasswordChangeSuccess,
        ShellProfileManualLock,
        ShellProfileManualLockBusy,
        ShellProfileManualLockDenied,
        ShellProfileManualLockError,
        ShellProfileManualLockSuccess,
        ShellProfileLogout,
        ShellProfileLogoutBusy,
        ShellProfileLogoutDenied,
        ShellProfileLogoutError,
        ShellProfileLogoutSuccess,
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
        AboutArtifactMode,
        AboutArtifactModeInstalled,
        AboutArtifactModePortableSupport,
        AboutDeviceCode,
        AboutDeviceCodeExplanation,
        AboutDeviceCodeCopy,
        AboutDeviceCodeCopied,
        AboutDeviceCodeCopyError,
        AboutPlugins,
        AboutPluginsEmpty,
        AboutSupportContact,
        AboutLegalLine,
        AboutOwnerIdentity,
        ShellCancelOperation,
        AboutOwnerStatusManaged,
        AboutOwnerStatusRegistered,
        AuditRetentionSweep
    ];
}

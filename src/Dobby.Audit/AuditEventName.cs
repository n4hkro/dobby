namespace Dobby.Audit;

public static class AuditEventName
{
    public const string UserLogin = "user.login";
    public const string UserLoginFailed = "user.login.failed";
    public const string UserLogout = "user.logout";
    public const string SessionLock = "session.lock";
    public const string SessionUnlock = "session.unlock";
    public const string SessionExpired = "session.expired";
    public const string SessionInvalidated = "session.invalidated";
    public const string UserCreate = "user.create";
    public const string UserUpdate = "user.update";
    public const string UserPasswordChange = "user.password.change";
    public const string UserPasswordReset = "user.password.reset";
    public const string UserActivate = "user.activate";
    public const string UserDeactivate = "user.deactivate";
    public const string PermissionGrant = "permission.grant";
    public const string PermissionRevoke = "permission.revoke";
    public const string PermissionDenied = "permission.denied";
    public const string OwnerProtectionDenied = "owner.protection.denied";
    public const string PluginInstallAttempted = "plugin.install.attempted";
    public const string PluginInstallVerified = "plugin.install.verified";
    public const string PluginInstallRejected = "plugin.install.rejected";
    public const string PluginInstallSignatureFailed = "plugin.install.signature-failed";
    public const string PluginInstallTrustFailed = "plugin.install.trust-failed";
    public const string PluginEnable = "plugin.enable";
    public const string PluginDisable = "plugin.disable";
    public const string PluginUninstall = "plugin.uninstall";
    public const string PluginActivationSkipped = "plugin.activation.skipped";
    public const string PluginActivationRejected = "plugin.activation.rejected";
    public const string PluginRuntimeFaulted = "plugin.runtime.faulted";
    public const string PluginLifecycleV1AdapterUsed = "plugin.lifecycle.v1-adapter-used";
    public const string RecoveryExport = "recovery.export";
    public const string RecoveryImport = "recovery.import";
    public const string LicenseImportAttempted = "license.import.attempted";
    public const string LicenseImportSucceeded = "license.import.succeeded";
    public const string LicenseImportRejected = "license.import.rejected";
    public const string LicenseEnforcementDenied = "license.enforcement.denied";
    public const string LicenseEnforcementWarning = "license.enforcement.warning";
    public const string CustomerDeploymentContextProvision = "customer-deployment-context.provision";
    public const string DiagnosticsExportCreated = "diagnostics.export.created";
    public const string SecurityVerificationFailed = "security.verification.failed";

    public static IReadOnlyList<string> All { get; } =
    [
        UserLogin,
        UserLoginFailed,
        UserLogout,
        SessionLock,
        SessionUnlock,
        SessionExpired,
        SessionInvalidated,
        UserCreate,
        UserUpdate,
        UserPasswordChange,
        UserPasswordReset,
        UserActivate,
        UserDeactivate,
        PermissionGrant,
        PermissionRevoke,
        PermissionDenied,
        OwnerProtectionDenied,
        PluginInstallAttempted,
        PluginInstallVerified,
        PluginInstallRejected,
        PluginInstallSignatureFailed,
        PluginInstallTrustFailed,
        PluginEnable,
        PluginDisable,
        PluginUninstall,
        PluginActivationSkipped,
        PluginActivationRejected,
        PluginRuntimeFaulted,
        PluginLifecycleV1AdapterUsed,
        RecoveryExport,
        RecoveryImport,
        LicenseImportAttempted,
        LicenseImportSucceeded,
        LicenseImportRejected,
        LicenseEnforcementDenied,
        LicenseEnforcementWarning,
        CustomerDeploymentContextProvision,
        DiagnosticsExportCreated,
        SecurityVerificationFailed
    ];
}

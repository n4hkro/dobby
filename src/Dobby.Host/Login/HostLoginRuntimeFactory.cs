using Dobby.Audit;
using Dobby.Application;
using Dobby.Configuration;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Diagnostics;
using Dobby.Host.Settings;
using Dobby.Host.Plugins;
using Dobby.Host.Startup;
using Dobby.Host.Sessions;
using Dobby.Infrastructure.HardwareIdentity;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.Printing;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Licensing;
using Dobby.Messaging;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authentication;
using Dobby.Security.Authorization;
using Dobby.Security.CurrentContext;
using Dobby.Security.Trust;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.IO;

namespace Dobby.Host.Login;

public static class HostLoginRuntimeFactory
{
    public static HostLoginRuntime Create(
        IPluginWorkSessionService? pluginWorkSessions = null,
        bool applyPreLoginMigrations = true,
        SessionRecord? activeSession = null)
    {
        var configuration = HostConfigurationSources.Build(new HostConfigurationSourceOptions(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            DefaultRuntimeConfigDirectory(),
            "Production",
            null));
        var options = HostConfigurationSources.BindDobbyOptions(configuration);
        ValidateOptions(options);
        return Create(
            options,
            TimeProvider.System,
            null,
            null,
            pluginWorkSessions: pluginWorkSessions,
            applyPreLoginMigrations: applyPreLoginMigrations,
            activeSession: activeSession);
    }

    public static HostPreLoginRuntime CreatePreLogin(bool applyPreLoginMigrations = true)
    {
        var configuration = HostConfigurationSources.Build(new HostConfigurationSourceOptions(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            DefaultRuntimeConfigDirectory(),
            "Production",
            null));
        var options = HostConfigurationSources.BindDobbyOptions(configuration);
        ValidateOptions(options);
        return CreatePreLogin(
            options,
            TimeProvider.System,
            null,
            null,
            applyPreLoginMigrations: applyPreLoginMigrations);
    }

    public static HostPreLoginRuntime CreatePreLogin(
        DobbyOptions options,
        TimeProvider timeProvider,
        IProtectedDataProtector? credentialProtector = null,
        IProtectedDataProtector? rememberMeProtector = null,
        bool applyPreLoginMigrations = true)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ValidateOptions(options);

        var protectedMaterialProbe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(options.Runtime.DataRoot);
        if (!protectedMaterialProbe.IsAvailable())
        {
            throw new InvalidOperationException("Protected material is unavailable.");
        }

        var protectedMaterial = protectedMaterialProbe.LoadRequired();
        var applicationEntropy = protectedMaterial.ApplicationEntropy;
        var persistenceOptions = new HostPersistenceOptions(
            options.Runtime.DataRoot,
            options.Storage.ProviderMode.ToString(),
            options.Storage.RequireDataAtRestProtection,
            options.Storage.BusyTimeoutMilliseconds);
        var factory = new EncryptedSqliteConnectionFactory(persistenceOptions, protectedMaterialProbe);
        var migrator = new HostPersistenceMigrator(factory, protectedMaterialProbe);
        if (applyPreLoginMigrations)
        {
            var migration = migrator.ApplyPreLoginMigrations();
            if (!migration.IsSuccess)
            {
                throw new InvalidOperationException(migration.Error.Message);
            }
        }

        var executor = new HostPersistenceCommandExecutor(factory);
        var transactionScope = new HostPersistenceTransactionScope(factory);
        var credentialScopeProtector = credentialProtector ?? new DpapiLocalMachineProtector();
        var rememberScopeProtector = rememberMeProtector ?? new DpapiCurrentUserProtector();
        var kdfPolicyResolver = new KdfPolicyResolver(options.Auth);
        var credentialStore = new CredentialStore(executor, credentialScopeProtector, kdfPolicyResolver, applicationEntropy);
        var auditTrail = new DurableAuditTrail(executor);
        var ownerBootstrapCoordinator = new OwnerBootstrapCoordinator(
            transactionScope,
            new OwnerBootstrapService(credentialScopeProtector, kdfPolicyResolver, applicationEntropy),
            auditTrail,
            timeProvider);
        var throttleService = new LoginThrottleService(options.Auth.LoginThrottleStepsSeconds, timeProvider);
        var loginService = new LoginService(executor, credentialStore, ownerBootstrapCoordinator, throttleService);
        var sessionLifecycleService = new SessionLifecycleService(
            new SessionStore(executor),
            new ActiveSessionAccessor(),
            credentialStore,
            timeProvider,
            options.Session);
        var rememberMeSlotService = new RememberMeSlotService(
            executor,
            rememberScopeProtector,
            applicationEntropy,
            StartupRememberMeSnapshotSink.Instance);
        var showDisplayNameInput = executor.QueryScalar<int>("SELECT COUNT(*) FROM Users;") == 0;
        var savedLogin = rememberMeSlotService.LoadSavedLogin();
        if (!savedLogin.IsSuccess)
        {
            throw new InvalidOperationException("Remember-me slot could not be loaded.");
        }

        var loginWorkflow = new LoginRuntimeWorkflow(
            loginService,
            sessionLifecycleService,
            rememberMeSlotService,
            new LoginAuditCoordinator(auditTrail, timeProvider),
            transactionScope);

        var fingerprintSource = new HostDeviceFingerprintSource();
        var recoveryRequestExporter = new RecoveryRequestExporter(
            fingerprintSource,
            new FixedHostVersionProvider(new HostVersion(0, 1, 0)),
            timeProvider);
        var recoveryImporter = new RecoveryResponseImporter(new VendorRecoveryPublicKey(), fingerprintSource);
        var recoveryRequestExportCoordinator = new RecoveryRequestExportCoordinator(
            recoveryRequestExporter,
            auditTrail);
        var recoveryResponseImportCoordinator = new RecoveryResponseImportCoordinator(
            transactionScope,
            executor,
            recoveryImporter,
            credentialScopeProtector,
            kdfPolicyResolver,
            auditTrail,
            timeProvider,
            applicationEntropy);
        var recoveryViewModel = new RecoveryViewModel(
            new WindowsRecoveryFilePicker(),
            new WindowsRecoveryExportFolderOpener(),
            new WindowsRecoveryExportPathClipboard(),
            recoveryRequestExportCoordinator,
            recoveryResponseImportCoordinator,
            new RecoveryPasswordResetPrompt());

        return new HostPreLoginRuntime(
            loginWorkflow,
            new LoginViewModel(
                savedLogin.Value,
                throttleService,
                showDisplayNameInput),
            recoveryViewModel);
    }

    public static HostLoginRuntime Create(
        DobbyOptions options,
        TimeProvider timeProvider,
        IProtectedDataProtector? credentialProtector = null,
        IProtectedDataProtector? rememberMeProtector = null,
        IBackgroundOperationReporter? backgroundOperations = null,
        IPluginWorkSessionService? pluginWorkSessions = null,
        bool applyPreLoginMigrations = true,
        SessionRecord? activeSession = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ValidateOptions(options);

        var protectedMaterialProbe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(options.Runtime.DataRoot);
        if (!protectedMaterialProbe.IsAvailable())
        {
            throw new InvalidOperationException("Protected material is unavailable.");
        }
        var protectedMaterial = protectedMaterialProbe.LoadRequired();
        var applicationEntropy = protectedMaterial.ApplicationEntropy;

        var persistenceOptions = new HostPersistenceOptions(
            options.Runtime.DataRoot,
            options.Storage.ProviderMode.ToString(),
            options.Storage.RequireDataAtRestProtection,
            options.Storage.BusyTimeoutMilliseconds);
        var factory = new EncryptedSqliteConnectionFactory(persistenceOptions, protectedMaterialProbe);
        var migrator = new HostPersistenceMigrator(factory, protectedMaterialProbe);
        if (applyPreLoginMigrations)
        {
            var migration = migrator.ApplyPreLoginMigrations();
            if (!migration.IsSuccess)
            {
                throw new InvalidOperationException(migration.Error.Message);
            }
        }

        var executor = new HostPersistenceCommandExecutor(factory);
        var transactionScope = new HostPersistenceTransactionScope(factory);
        var currentContextStore = new HostCustomerDeploymentContextStore(executor);
        var currentContextService = new CurrentCustomerDeploymentContextService(currentContextStore);
        var currentContext = currentContextService.EnsureDefaultContext();
        if (!currentContext.IsSuccess)
        {
            throw new InvalidOperationException("Current customer/deployment context could not be materialized.");
        }

        var licenseStateStore = new HostLicenseStateStore(executor, transactionScope);
        var currentLicenseDeviceFingerprint = new Lazy<string>(() => CreateCurrentLicenseDeviceFingerprint(protectedMaterial.StableDeviceId));
        var licenseRuntimeModeProvider = new LicenseRuntimeModeProvider(
            licenseStateStore,
            currentContextService,
            () => currentLicenseDeviceFingerprint.Value);
        var credentialScopeProtector = credentialProtector ?? new DpapiLocalMachineProtector();
        var rememberScopeProtector = rememberMeProtector ?? new DpapiCurrentUserProtector();
        var kdfPolicyResolver = new KdfPolicyResolver(options.Auth);
        var credentialStore = new CredentialStore(executor, credentialScopeProtector, kdfPolicyResolver, applicationEntropy);
        var auditTrail = new DurableAuditTrail(executor);
        var operationReporter = backgroundOperations ?? new BackgroundOperationReporter();
        AuditRetentionMaintenance.StartOneShot(
            executor,
            options.Audit.RetentionDays,
            operationReporter);
        var ownerBootstrapCoordinator = new OwnerBootstrapCoordinator(
            transactionScope,
            new OwnerBootstrapService(credentialScopeProtector, kdfPolicyResolver, applicationEntropy),
            auditTrail,
            timeProvider);
        var throttleService = new LoginThrottleService(options.Auth.LoginThrottleStepsSeconds, timeProvider);
        var loginService = new LoginService(executor, credentialStore, ownerBootstrapCoordinator, throttleService);
        var activeSessionAccessor = new ActiveSessionAccessor();
        if (activeSession is not null)
        {
            var active = activeSessionAccessor.SetActive(activeSession);
            if (!active.IsSuccess)
            {
                throw new InvalidOperationException("Authenticated session could not be activated for post-login runtime.");
            }
        }

        var sessionLifecycleService = new SessionLifecycleService(
            new SessionStore(executor),
            activeSessionAccessor,
            credentialStore,
            timeProvider,
            options.Session);
        var sessionAuditCoordinator = new SessionLifecycleAuditCoordinator(auditTrail, timeProvider);
        var lockRuntimeWorkflow = new LockRuntimeWorkflow(sessionLifecycleService, sessionAuditCoordinator);
        var pluginActivationCancellation = new HostPluginActivationCancellation();
        var rememberMeSlotService = new RememberMeSlotService(executor, rememberScopeProtector, applicationEntropy, StartupRememberMeSnapshotSink.Instance);
        var showDisplayNameInput = executor.QueryScalar<int>("SELECT COUNT(*) FROM Users;") == 0;
        var savedLogin = rememberMeSlotService.LoadSavedLogin();
        if (!savedLogin.IsSuccess)
        {
            throw new InvalidOperationException("Remember-me slot could not be loaded.");
        }

        var loginWorkflow = new LoginRuntimeWorkflow(
            loginService,
            sessionLifecycleService,
            rememberMeSlotService,
            new LoginAuditCoordinator(auditTrail, timeProvider),
            transactionScope);

        var fingerprintSource = new HostDeviceFingerprintSource();
        var recoveryRequestExporter = new RecoveryRequestExporter(
            fingerprintSource,
            new FixedHostVersionProvider(new HostVersion(0, 1, 0)),
            timeProvider);
        var recoveryImporter = new RecoveryResponseImporter(new VendorRecoveryPublicKey(), fingerprintSource);
        var recoveryRequestExportCoordinator = new RecoveryRequestExportCoordinator(
            recoveryRequestExporter,
            auditTrail);
        var recoveryResponseImportCoordinator = new RecoveryResponseImportCoordinator(
            transactionScope,
            executor,
            recoveryImporter,
            credentialScopeProtector,
            kdfPolicyResolver,
            auditTrail,
            timeProvider,
            applicationEntropy);
        var passwordChangeCoordinator = new SelfServicePasswordChangeCoordinator(
            executor,
            credentialStore,
            rememberMeSlotService,
            auditTrail);
        var operationTargetRegistry = OperationTargetRegistry.CreateDefault();
        var permissionGrantStore = new PermissionGrantStore(executor);
        var operationAuthorizationPolicy = new OperationAuthorizationPolicy(operationTargetRegistry, permissionGrantStore);
        var userManagementGuard = new UserManagementAuthorizationGuard(operationAuthorizationPolicy, permissionGrantStore, operationTargetRegistry);
        var userManagementAuditCoordinator = new UserManagementAuditCoordinator(auditTrail, executor, timeProvider);
        var pluginExtensionPointRegistry = new PluginExtensionPointRegistry();
        var pluginLifetimeManager = new PluginLifetimeManager();
        var installedPluginStateStore = new HostInstalledPluginStateStore(executor, transactionScope);
        var installedPluginMetadataCache = new HostInstalledPluginMetadataCache(executor, transactionScope);
        var pluginInstallStagingArea = new PluginInstallStagingArea(options.Runtime.DataRoot, executor);
        var pluginDataRetentionStore = new PluginDataRetentionStore(options.Runtime.DataRoot);
        var pluginRuntimeAuditCoordinator = new PluginRuntimeAuditCoordinator(auditTrail, timeProvider);
        var pluginRuntimeDiagnosticsCoordinator = new PluginRuntimeDiagnosticsCoordinator(_ => { });
        var pluginCurrentProcessActivationTracker = new PluginCurrentProcessActivationTracker();
        var pluginTrustVerifier = new PluginTrustVerifier();
        var licenseImportService = new LicenseImportService(
            new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider()),
            currentContextService,
            licenseStateStore);
        var licenseImportCoordinator = new LicenseImportCoordinator(
            licenseImportService,
            installedPluginStateStore,
            operationAuthorizationPolicy,
            () => currentLicenseDeviceFingerprint.Value,
            auditTrail,
            timeProvider);
        var postLoginSessionRuntime = new PostLoginSessionRuntime(
            sessionLifecycleService,
            sessionAuditCoordinator,
            lockRuntimeWorkflow,
            new WpfLockWindowPresenter(),
            pluginActivationCancellation,
            licenseImportCoordinator);
        var pluginHostServices = new PluginHostServices(
            new InMemoryEventBus(),
            operationReporter,
            pluginActivationCancellation,
            pluginId => new PluginCapabilityLicensing(() =>
                ResolvePluginRuntimeMode(licenseRuntimeModeProvider, installedPluginStateStore, pluginId)),
            operationAuthorizationPolicy,
            pluginWorkSessions);
        var pluginPackageInstallCoordinator = new HostPluginPackageInstallCoordinator(
            installedPluginStateStore,
            installedPluginMetadataCache,
            pluginInstallStagingArea,
            operationAuthorizationPolicy,
            pluginRuntimeAuditCoordinator,
            pluginTrustVerifier,
            pluginRuntimeDiagnosticsCoordinator,
            pluginId => ResolvePluginRuntimeMode(licenseRuntimeModeProvider, installedPluginStateStore, pluginId));
        var pluginLifecycleCoordinator = new HostPluginLifecycleCoordinator(
            installedPluginStateStore,
            installedPluginMetadataCache,
            pluginInstallStagingArea,
            pluginDataRetentionStore,
            pluginCurrentProcessActivationTracker,
            operationAuthorizationPolicy,
            pluginRuntimeAuditCoordinator);
        var hostPluginRuntimeCoordinator = new HostPluginRuntimeCoordinator(
            migrator,
            installedPluginStateStore,
            installedPluginMetadataCache,
            pluginInstallStagingArea,
            pluginDataRetentionStore,
            operationTargetRegistry,
            pluginExtensionPointRegistry,
            pluginLifetimeManager,
            pluginActivationCancellation,
            new PostLoginPluginActivator(new PluginServiceRegistrationBridge(pluginExtensionPointRegistry, pluginLifetimeManager, pluginHostServices)),
            pluginCurrentProcessActivationTracker,
            pluginTrustVerifier,
            pluginRuntimeAuditCoordinator,
            operationReporter,
            pluginRuntimeDiagnosticsCoordinator,
            licenseRuntimeModeProvider);
        var managedUserService = new ManagedUserService(
            executor,
            credentialStore,
            permissionGrantStore,
            userManagementGuard,
            timeProvider,
            userManagementAuditCoordinator);
        var settingsPreferenceSource = new HostConfigurationSettingsPreferenceSource(executor);
        Func<string, ISettingsPreferenceSource> settingsPreferenceSourceFactory = userId =>
            new HostConfigurationSettingsPreferenceSource(
                executor,
                SettingsPreferenceScope.ForAuthenticatedUser(userId));
        var accountSettingsModel = new AccountSettingsModel(passwordChangeCoordinator.ChangePassword);
        var settingsNavigationModel = new SettingsNavigationModel(operationAuthorizationPolicy, settingsPreferenceSource);
        var aboutInfoProvider = new AboutInfoProvider(
            new AssemblyHostVersionProvider(typeof(HostLoginRuntimeFactory).Assembly),
            new HostSupportDeviceCodeProvider(fingerprintSource),
            "support@example.invalid",
            "Copyright Dobby",
            new AssemblyHostBuildMetadataProvider(typeof(HostLoginRuntimeFactory).Assembly));
        var recoveryViewModel = new RecoveryViewModel(
            new WindowsRecoveryFilePicker(),
            new WindowsRecoveryExportFolderOpener(),
            new WindowsRecoveryExportPathClipboard(),
            recoveryRequestExportCoordinator,
            recoveryResponseImportCoordinator,
            new RecoveryPasswordResetPrompt());
        var diagnosticsExportCoordinator = new ShellDiagnosticsExportCoordinator(
            new DiagnosticsExportPackageBuilder(),
            auditTrail,
            timeProvider);
        var diagnosticsOutputZipPath = Path.Combine(options.Runtime.DataRoot, "temp", "dobby-diagnostics.zip");
        var printTargetClassifier = new WindowsPrintTargetClassifierAdapter(new WindowsPrintTargetClassifier());

        return new HostLoginRuntime(
            loginWorkflow,
            new LoginViewModel(
                savedLogin.Value,
                throttleService,
                showDisplayNameInput),
            recoveryViewModel,
            sessionLifecycleService,
            postLoginSessionRuntime,
            recoveryRequestExportCoordinator,
            recoveryResponseImportCoordinator,
            passwordChangeCoordinator,
            operationAuthorizationPolicy,
            managedUserService,
            permissionGrantStore.LoadUserState,
            settingsPreferenceSource,
            settingsPreferenceSourceFactory,
            accountSettingsModel,
            settingsNavigationModel,
            aboutInfoProvider,
            diagnosticsExportCoordinator,
            pluginPackageInstallCoordinator,
            pluginLifecycleCoordinator,
            licenseImportCoordinator,
            hostPluginRuntimeCoordinator,
            currentContextService,
            licenseStateStore,
            licenseRuntimeModeProvider,
            printTargetClassifier,
            operationReporter,
            options.Runtime.DataRoot,
            diagnosticsOutputZipPath);
    }

    private static string CreateCurrentLicenseDeviceFingerprint(byte[] stableDeviceId)
    {
        var hardware = new WindowsHardwareIdentityProvider().Read();
        var fingerprint = LicenseDeviceFingerprintV1.Compute(hardware.MachineGuid, stableDeviceId);
        if (!fingerprint.IsSuccess)
        {
            throw new InvalidOperationException("License device fingerprint could not be computed.");
        }

        return fingerprint.Value.Value;
    }

    private static Result<PluginRuntimeMode> ResolvePluginRuntimeMode(
        ILicenseRuntimeModeProvider licenseRuntimeModeProvider,
        IInstalledPluginStateStore installedPluginStateStore,
        string pluginId)
    {
        var snapshot = installedPluginStateStore.Snapshot();
        if (!snapshot.IsSuccess)
        {
            return Result.Failure<PluginRuntimeMode>(snapshot.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        var installed = snapshot.Value.SingleOrDefault(state =>
            state.PluginId.Equals(pluginId, StringComparison.Ordinal));
        return installed is null
            ? Result.Success(PluginRuntimeMode.Denied)
            : licenseRuntimeModeProvider.GetCurrentMode(pluginId, installed.PackageFingerprint);
    }

    private static void ValidateOptions(DobbyOptions options)
    {
        var result = new DobbyOptionsValidator().Validate(Options.DefaultName, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(DobbyOptions), result.Failures);
        }
    }

    private static string DefaultRuntimeConfigDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Dobby", "config");
    }
}

internal sealed class HostDeviceFingerprintSource : IDeviceFingerprintSource
{
    public Result<DeviceFingerprint> Current()
    {
        return new DeviceFingerprintComposer(new WindowsHardwareIdentityProvider()).Compose();
    }
}

internal sealed class HostSupportDeviceCodeProvider(IDeviceFingerprintSource fingerprintSource) : ISupportDeviceCodeProvider
{
    public string CurrentRedactedCode()
    {
        var fingerprint = fingerprintSource.Current();
        if (!fingerprint.IsSuccess)
        {
            return "device-unavailable";
        }

        var machine = fingerprint.Value.MachineGuid;
        return machine.Length <= 12 ? machine : machine[..12];
    }
}

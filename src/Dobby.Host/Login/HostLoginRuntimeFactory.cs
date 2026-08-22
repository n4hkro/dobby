using Dobby.Audit;
using Dobby.Application;
using Dobby.Configuration;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Contracts.PluginStorage;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Diagnostics;
using Dobby.Host.Settings;
using Dobby.Host.Plugins;
using Dobby.Host.Printing;
using Dobby.Host.Startup;
using Dobby.Host.Sessions;
using Dobby.Infrastructure.HardwareIdentity;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.Printing;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Licensing;
using Dobby.Localization;
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
            applyPreLoginMigrations: applyPreLoginMigrations);
    }

    public static HostPreLoginRuntime CreatePreLogin(
        DobbyOptions options,
        TimeProvider timeProvider,
        IProtectedDataProtector? credentialProtector = null,
        bool applyPreLoginMigrations = true,
        ILoginRuntimeCompositionProbe? compositionProbe = null)
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
        var kdfPolicyResolver = new KdfPolicyResolver(options.Auth);
        var credentialStore = new CredentialStore(
            executor,
            credentialScopeProtector,
            kdfPolicyResolver,
            applicationEntropy,
            verificationGate: new CredentialVerificationGate(options.Auth.MaxConcurrentPasswordVerifications));
        var auditTrail = new DurableAuditTrail(executor);
        var ownerBootstrapCoordinator = new OwnerBootstrapCoordinator(
            transactionScope,
            new OwnerBootstrapService(credentialScopeProtector, kdfPolicyResolver, applicationEntropy),
            auditTrail,
            timeProvider);
        var throttleStore = new HostLoginThrottleStore(transactionScope);
        var throttleService = CreateLoginThrottleService(options.Auth, timeProvider, throttleStore);
        var loginService = new LoginService(executor, kdfPolicyResolver, credentialStore, ownerBootstrapCoordinator, throttleService);
        var sessionLifecycleService = new SessionLifecycleService(
            new SessionStore(executor),
            new ActiveSessionAccessor(),
            credentialStore,
            timeProvider,
            options.Session);
        var rememberMeSlotService = new RememberMeSlotService(executor);
        var showDisplayNameInput = executor.QueryScalar<int>("SELECT COUNT(*) FROM Users;") == 0;
        var savedLogin = rememberMeSlotService.LoadRememberedAccount();
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

        var mutationUnitOfWork = new HostMutationUnitOfWork(
            transactionScope,
            auditTrail,
            new HostTransactionOutbox(timeProvider));
        var deviceRecoveryKey = new DeviceRecoveryKeySigner(
            protectedMaterial.DeviceRecoveryPrivateKeyPkcs8
                ?? throw new InvalidOperationException("Device recovery private key is unavailable."),
            protectedMaterial.DeviceRecoveryPublicKeySpki
                ?? throw new InvalidOperationException("Device recovery public key is unavailable."));
        var recoveryAuthority = ResolvePreAuthRecoveryAuthority(
            executor,
            protectedMaterial.StableDeviceId);
        var recoveryRequestExporter = new RecoveryRequestExporter(
            deviceRecoveryKey,
            recoveryAuthority,
            timeProvider,
            VendorRecoveryPublicKey.ProductionKeyId);
        var recoveryImporter = new RecoveryResponseImporter(new VendorRecoveryPublicKey());
        var recoveryRequestExportCoordinator = new RecoveryRequestExportCoordinator(
            recoveryRequestExporter,
            mutationUnitOfWork,
            timeProvider);
        var recoveryResponseImportCoordinator = new RecoveryResponseImportCoordinator(
            executor,
            recoveryImporter,
            credentialStore,
            mutationUnitOfWork,
            auditTrail,
            timeProvider,
            recoveryAuthority,
            deviceRecoveryKey.PublicKeyHash);
        var recoveryViewModel = new RecoveryViewModel(
            new WindowsRecoveryFilePicker(),
            new WindowsRecoveryExportFolderOpener(),
            new WindowsRecoveryExportPathClipboard(),
            recoveryRequestExportCoordinator,
            recoveryResponseImportCoordinator,
            new RecoveryPasswordResetPrompt());

        var runtime = new HostPreLoginRuntime(
            loginWorkflow,
            new LoginViewModel(
                savedLogin.Value,
                throttleService,
                showDisplayNameInput),
            recoveryViewModel);
        compositionProbe?.Record(LoginRuntimeCompositionEvents.PreAuthRuntimeConstructed);
        return runtime;
    }

    public static HostLoginRuntime CreateAuthenticated(
        SessionRecord authenticatedSession,
        IPluginWorkSessionService? pluginWorkSessions = null,
        bool applyPreLoginMigrations = true)
    {
        ArgumentNullException.ThrowIfNull(authenticatedSession);

        var configuration = HostConfigurationSources.Build(new HostConfigurationSourceOptions(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            DefaultRuntimeConfigDirectory(),
            "Production",
            null));
        var options = HostConfigurationSources.BindDobbyOptions(configuration);
        ValidateOptions(options);
        return CreateAuthenticated(
            options,
            TimeProvider.System,
            authenticatedSession,
            pluginWorkSessions: pluginWorkSessions,
            applyPreLoginMigrations: applyPreLoginMigrations);
    }

    public static HostLoginRuntime CreateAuthenticated(
        DobbyOptions options,
        TimeProvider timeProvider,
        SessionRecord authenticatedSession,
        IProtectedDataProtector? credentialProtector = null,
        IBackgroundOperationReporter? backgroundOperations = null,
        IPluginWorkSessionService? pluginWorkSessions = null,
        bool applyPreLoginMigrations = true,
        ILoginRuntimeCompositionProbe? compositionProbe = null)
    {
        ArgumentNullException.ThrowIfNull(authenticatedSession);
        return Create(
            options,
            timeProvider,
            credentialProtector,
            backgroundOperations,
            pluginWorkSessions,
            applyPreLoginMigrations,
            authenticatedSession,
            compositionProbe);
    }

    public static HostLoginRuntime Create(
        DobbyOptions options,
        TimeProvider timeProvider,
        IProtectedDataProtector? credentialProtector = null,
        IBackgroundOperationReporter? backgroundOperations = null,
        IPluginWorkSessionService? pluginWorkSessions = null,
        bool applyPreLoginMigrations = true,
        SessionRecord? activeSession = null,
        ILoginRuntimeCompositionProbe? compositionProbe = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ValidateOptions(options);
        compositionProbe?.Record(LoginRuntimeCompositionEvents.AuthenticatedRuntimeConstructionStarted);

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
        compositionProbe?.Record(LoginRuntimeCompositionEvents.BusinessRepositoriesConstructed);
        var currentContextStore = new HostCustomerDeploymentContextStore(executor, transactionScope);
        var currentContextService = new CurrentCustomerDeploymentContextService(currentContextStore);
        var currentContext = currentContextService.EnsureDefaultContext();
        if (!currentContext.IsSuccess)
        {
            throw new InvalidOperationException("Current customer/deployment context could not be materialized.");
        }
        var credentialScopeProtector = credentialProtector ?? new DpapiLocalMachineProtector();
        var kdfPolicyResolver = new KdfPolicyResolver(options.Auth);
        var credentialStore = new CredentialStore(
            executor,
            credentialScopeProtector,
            kdfPolicyResolver,
            applicationEntropy,
            verificationGate: new CredentialVerificationGate(options.Auth.MaxConcurrentPasswordVerifications));
        var auditTrail = new DurableAuditTrail(executor);
        var mutationUnitOfWork = new HostMutationUnitOfWork(
            transactionScope,
            auditTrail,
            new HostTransactionOutbox(timeProvider));
        var licenseStateStore = new HostLicenseStateStore(
            executor,
            transactionScope,
            mutationUnitOfWork);
        compositionProbe?.Record(LoginRuntimeCompositionEvents.LicensingConstructed);
        var currentLicenseDeviceFingerprint = new Lazy<string>(() => CreateCurrentLicenseDeviceFingerprint(protectedMaterial.StableDeviceId));
        var licenseRuntimeModeProvider = new LicenseRuntimeModeProvider(
            licenseStateStore,
            currentContextService,
            () => currentLicenseDeviceFingerprint.Value);
        var operationReporter = backgroundOperations ?? new BackgroundOperationReporter();
        var ownerBootstrapCoordinator = new OwnerBootstrapCoordinator(
            transactionScope,
            new OwnerBootstrapService(credentialScopeProtector, kdfPolicyResolver, applicationEntropy),
            auditTrail,
            timeProvider);
        var throttleStore = new HostLoginThrottleStore(transactionScope);
        var throttleService = CreateLoginThrottleService(options.Auth, timeProvider, throttleStore);
        var loginService = new LoginService(executor, kdfPolicyResolver, credentialStore, ownerBootstrapCoordinator, throttleService);
        var activeSessionAccessor = new ActiveSessionAccessor();
        if (activeSession is not null)
        {
            var active = activeSessionAccessor.SetActive(activeSession);
            if (!active.IsSuccess)
            {
                throw new InvalidOperationException("Authenticated session could not be activated for post-login runtime.");
            }
        }

        var sessionOptions = ResolveSessionOptions(options.Session, executor, activeSession?.UserId);
        var sessionAuditCoordinator = new SessionLifecycleAuditCoordinator(
            auditTrail,
            timeProvider,
            mutationUnitOfWork);
        var sessionLifecycleService = new SessionLifecycleService(
            new SessionStore(executor),
            activeSessionAccessor,
            credentialStore,
            timeProvider,
            sessionOptions,
            sessionAuditCoordinator);
        var lockThrottleService = CreateLoginThrottleService(options.Auth, timeProvider, throttleStore);
        var passwordChangeThrottleService = CreateLoginThrottleService(options.Auth, timeProvider, throttleStore);
        var lockRuntimeWorkflow = new LockRuntimeWorkflow(
            sessionLifecycleService,
            sessionAuditCoordinator,
            lockThrottleService);
        var pluginActivationCancellation = new HostPluginActivationCancellation();
        var rememberMeSlotService = new RememberMeSlotService(executor);
        var showDisplayNameInput = executor.QueryScalar<int>("SELECT COUNT(*) FROM Users;") == 0;
        var savedLogin = rememberMeSlotService.LoadRememberedAccount();
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

        var deviceRecoveryKey = new DeviceRecoveryKeySigner(
            protectedMaterial.DeviceRecoveryPrivateKeyPkcs8
                ?? throw new InvalidOperationException("Device recovery private key is unavailable."),
            protectedMaterial.DeviceRecoveryPublicKeySpki
                ?? throw new InvalidOperationException("Device recovery public key is unavailable."));
        var recoveryAuthority = new RecoveryAuthorityContext(
            currentContext.Context!.CustomerId,
            currentContext.Context.DeploymentId,
            DeviceRecoveryIdentity.ComputeDeviceId(protectedMaterial.StableDeviceId));
        var recoveryRequestExporter = new RecoveryRequestExporter(
            deviceRecoveryKey,
            recoveryAuthority,
            timeProvider,
            VendorRecoveryPublicKey.ProductionKeyId);
        var recoveryImporter = new RecoveryResponseImporter(new VendorRecoveryPublicKey());
        var recoveryRequestExportCoordinator = new RecoveryRequestExportCoordinator(
            recoveryRequestExporter,
            mutationUnitOfWork,
            timeProvider);
        var recoveryResponseImportCoordinator = new RecoveryResponseImportCoordinator(
            executor,
            recoveryImporter,
            credentialStore,
            mutationUnitOfWork,
            auditTrail,
            timeProvider,
            recoveryAuthority,
            deviceRecoveryKey.PublicKeyHash);
        var passwordChangeCoordinator = new SelfServicePasswordChangeCoordinator(
            executor,
            credentialStore,
            rememberMeSlotService,
            passwordChangeThrottleService,
            mutationUnitOfWork,
            auditTrail,
            timeProvider);
        var operationTargetRegistry = OperationTargetRegistry.CreateDefault();
        var permissionGrantStore = new PermissionGrantStore(executor);
        var operationAuthorizationPolicy = new OperationAuthorizationPolicy(operationTargetRegistry, permissionGrantStore);
        var customerDeploymentContextProvisioningCoordinator = new HostCustomerDeploymentContextProvisioningCoordinator(
            currentContextService,
            operationAuthorizationPolicy,
            auditTrail,
            timeProvider);
        var userManagementGuard = new UserManagementAuthorizationGuard(operationAuthorizationPolicy, permissionGrantStore, operationTargetRegistry);
        var userManagementAuditCoordinator = new UserManagementAuditCoordinator(
            auditTrail,
            executor,
            timeProvider,
            mutationUnitOfWork);
        var pluginExtensionPointRegistry = new PluginExtensionPointRegistry();
        compositionProbe?.Record(LoginRuntimeCompositionEvents.PluginRuntimeConstructed);
        var pluginLifetimeManager = new PluginLifetimeManager();
        var installedPluginStateStore = new HostInstalledPluginStateStore(executor, transactionScope);
        var installedPluginMetadataCache = new HostInstalledPluginMetadataCache(executor, transactionScope);
        var pluginActivationStatusPersistence = new HostPluginActivationStatusPersistence(
            installedPluginStateStore,
            installedPluginMetadataCache,
            transactionScope);
        var pluginInstallStagingArea = new PluginInstallStagingArea(options.Runtime.DataRoot, executor);
        var pluginDataRetentionStore = new PluginDataRetentionStore(options.Runtime.DataRoot, transactionScope);
        var pluginRuntimeAuditCoordinator = new PluginRuntimeAuditCoordinator(auditTrail, timeProvider);
        var pluginRuntimeDiagnosticsFileSink = new PluginRuntimeDiagnosticsFileSink(
            RuntimeFolderLayout.DiagnosticsLogs(options.Runtime));
        var pluginRuntimeDiagnosticsCoordinator = new PluginRuntimeDiagnosticsCoordinator(
            pluginRuntimeDiagnosticsFileSink.Record);
        var pluginCurrentProcessActivationTracker = new PluginCurrentProcessActivationTracker();
        var pluginTrustVerifier = new PluginTrustVerifier();
        var licenseImportService = new LicenseImportService(
            new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
                purpose: ArtifactSignaturePurposes.PluginLicense)),
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
        var runtimeModeMutationGate = new HostRuntimeModeMutationGate(
            pluginId => ResolvePluginRuntimeMode(licenseRuntimeModeProvider, installedPluginStateStore, pluginId));
        var pluginHostServices = new PluginHostServices(
            new InMemoryEventBus(),
            operationReporter,
            pluginActivationCancellation,
            pluginId => new PluginCapabilityLicensing(() =>
                ResolvePluginRuntimeMode(licenseRuntimeModeProvider, installedPluginStateStore, pluginId)),
            operationAuthorizationPolicy,
            pluginWorkSessions,
            pluginId => activeSessionAccessor.Current is null
                ? null
                : new HostPluginStorage(
                    pluginId,
                    executor,
                    transactionScope,
                    () => activeSessionAccessor.Current?.UserId ?? string.Empty,
                    runtimeModeMutationGate),
            pluginRuntimeDiagnosticsCoordinator.RecordEventHandlerFault);
        var pluginPackageInstallCoordinator = new HostPluginPackageInstallCoordinator(
            installedPluginStateStore,
            installedPluginMetadataCache,
            pluginInstallStagingArea,
            operationAuthorizationPolicy,
            pluginRuntimeAuditCoordinator,
            pluginTrustVerifier,
            runtimeModeMutationGate,
            pluginRuntimeDiagnosticsCoordinator,
            new PluginPackageLimits(
                options.Plugins.MaximumCompressedPackageMegabytes * 1024L * 1024,
                options.Plugins.MaximumManifestKilobytes * 1024,
                options.Plugins.MaximumEntryCount,
                options.Plugins.MaximumEntryUncompressedMegabytes * 1024L * 1024,
                options.Plugins.MaximumAggregateUncompressedMegabytes * 1024L * 1024,
                options.Plugins.MaximumExpansionRatio));
        var pluginLifecycleCoordinator = new HostPluginLifecycleCoordinator(
            installedPluginStateStore,
            installedPluginMetadataCache,
            pluginInstallStagingArea,
            pluginDataRetentionStore,
            pluginCurrentProcessActivationTracker,
            operationAuthorizationPolicy,
            pluginRuntimeAuditCoordinator,
            runtimeModeMutationGate);
        var hostPluginRuntimeCoordinator = new HostPluginRuntimeCoordinator(
            migrator,
            installedPluginStateStore,
            installedPluginMetadataCache,
            pluginActivationStatusPersistence,
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
            new HostSupportDeviceCodeProvider(
                protectedMaterial.StableDeviceId,
                protectedMaterial.ApplicationEntropy),
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
        var printOutputCoordinator = new HostPrintOutputCoordinator(
            printTargetClassifier,
            new WpfPhysicalPrintConfirmation(HostStringCatalog.CreateResourceBacked()));
        var printOutputService = new HostPrintOutputService(
            licenseRuntimeModeProvider,
            printOutputCoordinator);
        var authenticatedLifetime = new CancellationTokenSource();
        var licenseActivationRefreshHandler = new LicenseActivationRefreshOutboxHandler();
        var outboxDispatcher = new HostOutboxDispatcher(
            transactionScope,
            new Dictionary<HostOutboxRoute, IHostOutboxMessageHandler>
            {
                [LicenseActivationRefreshOutboxHandler.Route] = licenseActivationRefreshHandler
            },
            timeProvider);
        var auditRetentionTask = AuditRetentionMaintenance.StartOneShot(
            executor,
            options.Audit.RetentionDays,
            operationReporter,
            authenticatedLifetime.Token);
        compositionProbe?.Record(LoginRuntimeCompositionEvents.AuditRetentionStarted);

        var runtime = new HostLoginRuntime(
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
            runtimeModeMutationGate,
            licenseImportCoordinator,
            hostPluginRuntimeCoordinator,
            currentContextService,
            customerDeploymentContextProvisioningCoordinator,
            licenseStateStore,
            licenseRuntimeModeProvider,
            printTargetClassifier,
            printOutputCoordinator,
            printOutputService,
            operationReporter,
            Path.Combine(options.Runtime.DataRoot, "logs"),
            diagnosticsOutputZipPath)
        {
            AuthenticatedLifetime = authenticatedLifetime,
            AuditRetentionTask = auditRetentionTask,
            LicenseActivationRefreshHandler = licenseActivationRefreshHandler,
            OutboxDispatcher = outboxDispatcher
        };
        compositionProbe?.Record(LoginRuntimeCompositionEvents.AuthenticatedRuntimeConstructed);
        return runtime;
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

    private static SessionOptions ResolveSessionOptions(
        SessionOptions defaults,
        IHostPersistenceCommandExecutor executor,
        string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return defaults;
        }

        var scopedPreferences = new HostConfigurationSettingsPreferenceSource(
            executor,
            SettingsPreferenceScope.ForAuthenticatedUser(userId)).Load();
        if (!scopedPreferences.IsSuccess)
        {
            return defaults;
        }

        var securitySession = scopedPreferences.Value.SecuritySession;
        return new SessionOptions
        {
            AbsoluteTimeoutMinutes = defaults.AbsoluteTimeoutMinutes,
            IdleTimeoutMinutes = securitySession.AutoLockEnabled
                ? securitySession.AutoLockMinutes
                : defaults.IdleTimeoutMinutes,
            AutoLockEnabledByDefault = securitySession.AutoLockEnabled
        };
    }

    private static RecoveryAuthorityContext ResolvePreAuthRecoveryAuthority(
        IHostPersistenceCommandExecutor executor,
        byte[] stableDeviceId)
    {
        var contexts = executor.Query<RecoveryAuthorityRow>(
            "SELECT CustomerId, DeploymentId FROM CustomerDeploymentContext;");
        if (contexts.Count > 1)
        {
            throw new InvalidOperationException("Recovery authority context is ambiguous.");
        }

        var customerId = contexts.Count == 0
            ? CurrentCustomerDeploymentContextService.DefaultCustomerId
            : contexts[0].CustomerId;
        var deploymentId = contexts.Count == 0
            ? CurrentCustomerDeploymentContextService.DefaultDeploymentId
            : contexts[0].DeploymentId;
        if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(deploymentId))
        {
            throw new InvalidOperationException("Recovery authority context is invalid.");
        }

        return new RecoveryAuthorityContext(
            customerId,
            deploymentId,
            DeviceRecoveryIdentity.ComputeDeviceId(stableDeviceId));
    }

    private sealed class RecoveryAuthorityRow
    {
        public string CustomerId { get; init; } = string.Empty;

        public string DeploymentId { get; init; } = string.Empty;
    }

    private static LoginThrottleService CreateLoginThrottleService(
        AuthOptions options,
        TimeProvider timeProvider,
        ILoginThrottleStore store)
    {
        return new LoginThrottleService(
            options.LoginThrottleStepsSeconds,
            timeProvider,
            store,
            options.LoginThrottleFailureWindowSeconds,
            options.LoginGlobalFailureThreshold,
            options.LoginGlobalBackoffSeconds);
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

internal sealed class HostSupportDeviceCodeProvider(
    byte[] stableDeviceId,
    byte[] supportCodeKey) : ISupportDeviceCodeProvider
{
    public string CurrentRedactedCode()
    {
        return SupportDeviceCodeV2.Compute(stableDeviceId, supportCodeKey);
    }
}

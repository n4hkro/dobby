using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Contracts.Licensing;
using Dobby.Host.Plugins;
using Dobby.Infrastructure.Persistence;
using Dobby.Licensing;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;
using Dobby.Security.Trust;
using Dobby.UnitTests.PluginFramework.Packaging;
using NSubstitute;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class HostPluginRuntimeCoordinatorTests
{
    [Fact]
    public void Constructor_requires_trust_verifier()
    {
        var extensionPointRegistry = new PluginExtensionPointRegistry();
        var lifetimeManager = new PluginLifetimeManager();

        Should.Throw<ArgumentNullException>(() =>
            new HostPluginRuntimeCoordinator(
                Substitute.For<IHostPersistenceMigrator>(),
                Substitute.For<IInstalledPluginStateStore>(),
                Substitute.For<IInstalledPluginMetadataCache>(),
                Substitute.For<IPluginInstallStagingArea>(),
                OperationTargetRegistry.CreateDefault(),
                extensionPointRegistry,
                lifetimeManager,
                new HostPluginActivationCancellation(),
                new PostLoginPluginActivator(new PluginServiceRegistrationBridge(extensionPointRegistry, lifetimeManager)),
                Substitute.For<IPluginCurrentProcessActivationTracker>(),
                pluginTrustVerifier: null!));
    }

    [Fact]
    public void Apply_post_login_migrations_recovers_staging_and_resets_activation_cancellation()
    {
        var migrator = Substitute.For<IHostPersistenceMigrator>();
        migrator.ApplyPostLoginMigrations(Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(PersistenceResult.Success());
        var staging = Substitute.For<IPluginInstallStagingArea>();
        staging.Recover().Returns(Result.Success(2));
        staging.CleanupRuntimeOrphans(Arg.Any<IReadOnlyCollection<string>>()).Returns(Result.Success(0));
        var activationCancellation = new HostPluginActivationCancellation();
        activationCancellation.CancelPostLoginActivation();
        var coordinator = CreateCoordinator(migrator: migrator, staging: staging, activationCancellation: activationCancellation);

        var result = coordinator.ApplyPostLoginMigrations();

        result.IsSuccess.ShouldBeTrue();
        staging.Received(1).Recover();
        activationCancellation.Token.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public void Apply_post_login_migrations_rebuilds_metadata_cache_from_authoritative_state()
    {
        var migrator = Substitute.For<IHostPersistenceMigrator>();
        migrator.ApplyPostLoginMigrations(Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(PersistenceResult.Success());
        var staging = Substitute.For<IPluginInstallStagingArea>();
        staging.Recover().Returns(Result.Success(0));
        staging.CleanupRuntimeOrphans(Arg.Any<IReadOnlyCollection<string>>()).Returns(Result.Success(0));
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        var states = new[]
        {
            CreateInstalledState("runtime", "fingerprint")
        };
        stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(states));
        var metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
        metadataCache.RebuildFrom(states).Returns(Result.Success());
        var coordinator = CreateCoordinator(
            migrator: migrator,
            stateStore: stateStore,
            staging: staging,
            metadataCache: metadataCache);

        var result = coordinator.ApplyPostLoginMigrations();

        result.IsSuccess.ShouldBeTrue();
        metadataCache.Received(1).RebuildFrom(states);
    }

    [Fact]
    public void Apply_post_login_migrations_deletes_pending_uninstall_runtime_before_cache_rebuild()
    {
        var migrator = Substitute.For<IHostPersistenceMigrator>();
        migrator.ApplyPostLoginMigrations(Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(PersistenceResult.Success());
        var pending = CreateInstalledState("pending-runtime", "pending-fingerprint") with
        {
            PluginId = "com.dobby.pending",
            IsEnabled = false,
            IsPendingUninstall = true,
            DataRetentionChoice = "delete",
            RestartRequired = true,
            StatusSummary = "Pending uninstall"
        };
        var retained = CreateInstalledState("retained-runtime", "retained-fingerprint") with
        {
            PluginId = "com.dobby.retained"
        };
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>([pending, retained]));
        stateStore.Remove(pending.PluginId).Returns(Result.Success());
        var metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
        metadataCache.Remove(pending.PluginId).Returns(Result.Success());
        metadataCache.RebuildFrom(Arg.Any<IReadOnlyList<InstalledPluginState>>()).Returns(Result.Success());
        var staging = Substitute.For<IPluginInstallStagingArea>();
        staging.Recover().Returns(Result.Success(0));
        staging.Cleanup(pending.RuntimePath).Returns(Result.Success());
        staging.CleanupRuntimeOrphans(Arg.Any<IReadOnlyCollection<string>>()).Returns(Result.Success(0));
        var coordinator = CreateCoordinator(
            migrator: migrator,
            stateStore: stateStore,
            staging: staging,
            metadataCache: metadataCache);

        var result = coordinator.ApplyPostLoginMigrations();

        result.IsSuccess.ShouldBeTrue();
        staging.Received(1).Cleanup(pending.RuntimePath);
        stateStore.Received(1).Remove(pending.PluginId);
        metadataCache.Received(1).Remove(pending.PluginId);
        metadataCache.Received(1).RebuildFrom(Arg.Is<IReadOnlyList<InstalledPluginState>>(states =>
            states.Count == 1 && states[0].PluginId == retained.PluginId));
    }

    [Fact]
    public void Apply_post_login_migrations_does_not_recover_staging_when_migration_fails()
    {
        var migrator = Substitute.For<IHostPersistenceMigrator>();
        migrator.ApplyPostLoginMigrations(Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.MigrationFailed,
                "migration failed")));
        var staging = Substitute.For<IPluginInstallStagingArea>();
        var coordinator = CreateCoordinator(migrator: migrator, staging: staging);

        var result = coordinator.ApplyPostLoginMigrations();

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe(PersistenceError.MigrationFailed);
        staging.DidNotReceive().Recover();
    }

    [Fact]
    public void Apply_post_login_migrations_fails_closed_when_staging_recovery_fails()
    {
        var migrator = Substitute.For<IHostPersistenceMigrator>();
        migrator.ApplyPostLoginMigrations(Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(PersistenceResult.Success());
        var staging = Substitute.For<IPluginInstallStagingArea>();
        staging.Recover().Returns(Result.Failure<int>(ErrorCode.IoFailed));
        var activationCancellation = new HostPluginActivationCancellation();
        activationCancellation.CancelPostLoginActivation();
        var coordinator = CreateCoordinator(migrator: migrator, staging: staging, activationCancellation: activationCancellation);

        var result = coordinator.ApplyPostLoginMigrations();

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe(PersistenceError.IoFailed);
        activationCancellation.Token.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public void Seed_operation_targets_adds_valid_plugin_contributions_to_security_registry()
    {
        var registry = OperationTargetRegistry.CreateDefault();
        var coordinator = CreateCoordinator(operationTargetRegistry: registry);
        var contribution = new PluginOperationTargetContribution(
            "com.dobby.sample",
            "com.dobby.sample.greet",
            [OperationVerb.Execute, OperationVerb.View],
            "sample.greet.label");

        var result = coordinator.SeedOperationTargets([contribution]);

        result.IsSuccess.ShouldBeTrue();
        var target = registry.Get("com.dobby.sample.greet").Value;
        target.LabelKey.ShouldBe("sample.greet.label");
        target.SupportedVerbs.ShouldBe([OperationVerb.Execute, OperationVerb.View]);
        target.Origin.ShouldBe(OperationTargetOrigin.Plugin);
    }

    [Fact]
    public void Activate_installed_plugins_reads_runtime_manifest_seeds_operation_targets_and_faults_unloadable_module()
    {
        var runtimePath = CreateRuntimePackageDirectory();
        try
        {
            var stateStore = Substitute.For<IInstalledPluginStateStore>();
            stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(
            [
                CreateInstalledState(runtimePath, RuntimePackageFingerprint(runtimePath))
            ]));
            var registry = OperationTargetRegistry.CreateDefault();
            var coordinator = CreateCoordinator(stateStore: stateStore, operationTargetRegistry: registry);

            var result = coordinator.ActivateInstalledPlugins(new HostVersion(1, 0, 0), CancellationToken.None);

            result.IsSuccess.ShouldBeTrue();
            var outcome = result.Value.Outcomes.Single();
            outcome.PluginId.ShouldBe(PluginPackageTestData.PluginId);
            outcome.Status.ShouldBe(PluginActivationStatus.Faulted);
            outcome.Reason.ShouldBe(ErrorCode.PluginInternalError.ToString());
            registry.Get($"{PluginPackageTestData.PluginId}.greet").IsSuccess.ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(runtimePath))
            {
                Directory.Delete(runtimePath, recursive: true);
            }
        }
    }

    [Fact]
    public void Activate_installed_plugins_marks_successful_activations_as_current_process_activated()
    {
        var runtimePath = CreateActivatableRuntimePackageDirectory();
        try
        {
            var stateStore = Substitute.For<IInstalledPluginStateStore>();
            stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(
            [
                CreateInstalledState(runtimePath, RuntimePackageFingerprint(runtimePath))
            ]));
            var activationTracker = Substitute.For<IPluginCurrentProcessActivationTracker>();
            var coordinator = CreateCoordinator(
                stateStore: stateStore,
                activationTracker: activationTracker);

            var result = coordinator.ActivateInstalledPlugins(new HostVersion(1, 0, 0), CancellationToken.None);

            result.IsSuccess.ShouldBeTrue(result.ErrorCode?.ToString());
            result.Value.ActivatedPluginIds.ShouldBe([PluginPackageTestData.PluginId]);
            activationTracker.Received(1).MarkActivatedInCurrentProcess(
                Arg.Is<IReadOnlyCollection<string>>(pluginIds =>
                    pluginIds.SequenceEqual(new[] { PluginPackageTestData.PluginId })));
        }
        finally
        {
            DeleteRuntimeDirectory(runtimePath);
        }
    }

    [Fact]
    public void Activate_installed_plugins_ignores_pending_uninstall_state_without_reading_runtime()
    {
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(
        [
            CreateInstalledState("missing-pending-runtime", "fingerprint") with
            {
                IsEnabled = false,
                IsPendingUninstall = true,
                RestartRequired = true,
                StatusSummary = "Pending uninstall"
            }
        ]));
        var registry = OperationTargetRegistry.CreateDefault();
        var coordinator = CreateCoordinator(stateStore: stateStore, operationTargetRegistry: registry);

        var result = coordinator.ActivateInstalledPlugins(new HostVersion(1, 0, 0), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Outcomes.ShouldBeEmpty();
        result.Value.ActivatedPluginIds.ShouldBeEmpty();
        result.Value.NavigationContributions.ShouldBeEmpty();
    }

    [Fact]
    public void Activate_installed_plugins_rejects_runtime_package_when_trust_revalidation_fails()
    {
        var runtimePath = CreateRuntimePackageDirectory();
        try
        {
            var stateStore = Substitute.For<IInstalledPluginStateStore>();
            stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(
            [
                CreateInstalledState(runtimePath, RuntimePackageFingerprint(runtimePath))
            ]));
            var registry = OperationTargetRegistry.CreateDefault();
            var coordinator = CreateCoordinator(
                stateStore: stateStore,
                operationTargetRegistry: registry,
                trustVerifier: new RejectingTrustVerifier("trust-failed", "not trusted"));

            var result = coordinator.ActivateInstalledPlugins(new HostVersion(1, 0, 0), CancellationToken.None);

            result.IsSuccess.ShouldBeTrue();
            var outcome = result.Value.Outcomes.Single();
            outcome.Status.ShouldBe(PluginActivationStatus.Rejected);
            outcome.Reason.ShouldBe("trust-failed");
            registry.Get($"{PluginPackageTestData.PluginId}.greet").IsSuccess.ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(runtimePath))
            {
                Directory.Delete(runtimePath, recursive: true);
            }
        }
    }

    [Fact]
    public void Activate_installed_plugins_rejects_runtime_package_when_installed_fingerprint_mismatches()
    {
        var runtimePath = CreateRuntimePackageDirectory();
        try
        {
            var stateStore = Substitute.For<IInstalledPluginStateStore>();
            stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(
            [
                CreateInstalledState(runtimePath, "stale-fingerprint")
            ]));
            var registry = OperationTargetRegistry.CreateDefault();
            var coordinator = CreateCoordinator(
                stateStore: stateStore,
                operationTargetRegistry: registry,
                trustVerifier: new AllowAllTrustVerifier());

            var result = coordinator.ActivateInstalledPlugins(new HostVersion(1, 0, 0), CancellationToken.None);

            result.IsSuccess.ShouldBeTrue();
            var outcome = result.Value.Outcomes.Single();
            outcome.Status.ShouldBe(PluginActivationStatus.Rejected);
            outcome.Reason.ShouldBe("package-fingerprint-mismatch");
            registry.Get($"{PluginPackageTestData.PluginId}.greet").IsSuccess.ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(runtimePath))
            {
                Directory.Delete(runtimePath, recursive: true);
            }
        }
    }

    [Fact]
    public void Activate_installed_plugins_rejects_denied_license_before_seeding_operation_targets()
    {
        var runtimePath = CreateRuntimePackageDirectory();
        try
        {
            var stateStore = Substitute.For<IInstalledPluginStateStore>();
            stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(
            [
                CreateInstalledState(runtimePath, RuntimePackageFingerprint(runtimePath))
            ]));
            var licenseRuntimeModeProvider = Substitute.For<ILicenseRuntimeModeProvider>();
            licenseRuntimeModeProvider
                .GetCurrentMode(PluginPackageTestData.PluginId, RuntimePackageFingerprint(runtimePath))
                .Returns(Result.Success(PluginRuntimeMode.Denied));
            var registry = OperationTargetRegistry.CreateDefault();
            var coordinator = CreateCoordinator(
                stateStore: stateStore,
                operationTargetRegistry: registry,
                licenseRuntimeModeProvider: licenseRuntimeModeProvider);

            var result = coordinator.ActivateInstalledPlugins(new HostVersion(1, 0, 0), CancellationToken.None);

            result.IsSuccess.ShouldBeTrue();
            var outcome = result.Value.Outcomes.Single();
            outcome.Status.ShouldBe(PluginActivationStatus.Rejected);
            outcome.Reason.ShouldBe("license-denied");
            registry.Get($"{PluginPackageTestData.PluginId}.greet").IsSuccess.ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(runtimePath))
            {
                Directory.Delete(runtimePath, recursive: true);
            }
        }
    }

    [Fact]
    public void Activate_installed_plugins_rejects_license_binding_mismatch_before_activation()
    {
        var runtimePath = CreateRuntimePackageDirectory();
        try
        {
            var stateStore = Substitute.For<IInstalledPluginStateStore>();
            stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(
            [
                CreateInstalledState(runtimePath, RuntimePackageFingerprint(runtimePath))
            ]));
            var licenseRuntimeModeProvider = Substitute.For<ILicenseRuntimeModeProvider>();
            licenseRuntimeModeProvider
                .GetCurrentMode(PluginPackageTestData.PluginId, RuntimePackageFingerprint(runtimePath))
                .Returns(Result.Failure<PluginRuntimeMode>(ErrorCode.LicenseBindingMismatch));
            var registry = OperationTargetRegistry.CreateDefault();
            var coordinator = CreateCoordinator(
                stateStore: stateStore,
                operationTargetRegistry: registry,
                licenseRuntimeModeProvider: licenseRuntimeModeProvider);

            var result = coordinator.ActivateInstalledPlugins(new HostVersion(1, 0, 0), CancellationToken.None);

            result.IsSuccess.ShouldBeTrue();
            var outcome = result.Value.Outcomes.Single();
            outcome.Status.ShouldBe(PluginActivationStatus.Rejected);
            outcome.Reason.ShouldBe("license-binding-mismatch");
            registry.Get($"{PluginPackageTestData.PluginId}.greet").IsSuccess.ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(runtimePath))
            {
                Directory.Delete(runtimePath, recursive: true);
            }
        }
    }

    [Fact]
    public void Activate_installed_plugins_allows_read_only_license_to_reach_activation_gates()
    {
        var runtimePath = CreateRuntimePackageDirectory();
        try
        {
            var stateStore = Substitute.For<IInstalledPluginStateStore>();
            stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(
            [
                CreateInstalledState(runtimePath, RuntimePackageFingerprint(runtimePath))
            ]));
            var licenseRuntimeModeProvider = Substitute.For<ILicenseRuntimeModeProvider>();
            licenseRuntimeModeProvider
                .GetCurrentMode(PluginPackageTestData.PluginId, RuntimePackageFingerprint(runtimePath))
                .Returns(Result.Success(PluginRuntimeMode.ReadOnlyExpired));
            var registry = OperationTargetRegistry.CreateDefault();
            var coordinator = CreateCoordinator(
                stateStore: stateStore,
                operationTargetRegistry: registry,
                licenseRuntimeModeProvider: licenseRuntimeModeProvider);

            var result = coordinator.ActivateInstalledPlugins(new HostVersion(1, 0, 0), CancellationToken.None);

            result.IsSuccess.ShouldBeTrue();
            var outcome = result.Value.Outcomes.Single();
            outcome.Status.ShouldBe(PluginActivationStatus.Faulted);
            outcome.Reason.ShouldBe(ErrorCode.PluginInternalError.ToString());
            registry.Get($"{PluginPackageTestData.PluginId}.greet").IsSuccess.ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(runtimePath))
            {
                Directory.Delete(runtimePath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Begin_post_login_activation_returns_existing_task_for_reentrant_calls()
    {
        var coordinator = CreateCoordinator();

        var first = coordinator.BeginPostLoginActivation(new HostVersion(1, 0, 0), "owner");
        var second = coordinator.BeginPostLoginActivation(new HostVersion(1, 0, 0), "owner");

        ReferenceEquals(first, second).ShouldBeTrue();
        var result = await first;
        result.IsSuccess.ShouldBeTrue();
    }

    private static HostPluginRuntimeCoordinator CreateCoordinator(
        IHostPersistenceMigrator? migrator = null,
        IInstalledPluginStateStore? stateStore = null,
        IInstalledPluginMetadataCache? metadataCache = null,
        IPluginInstallStagingArea? staging = null,
        OperationTargetRegistry? operationTargetRegistry = null,
        HostPluginActivationCancellation? activationCancellation = null,
        IPluginTrustVerifier? trustVerifier = null,
        IPluginCurrentProcessActivationTracker? activationTracker = null,
        ILicenseRuntimeModeProvider? licenseRuntimeModeProvider = null)
    {
        stateStore ??= CreateDefaultStateStore();
        metadataCache ??= CreateDefaultMetadataCache();
        staging ??= CreateDefaultStagingArea();

        var extensionPointRegistry = new PluginExtensionPointRegistry();
        var lifetimeManager = new PluginLifetimeManager();
        return new HostPluginRuntimeCoordinator(
            migrator ?? Substitute.For<IHostPersistenceMigrator>(),
            stateStore,
            metadataCache,
            staging,
            operationTargetRegistry ?? OperationTargetRegistry.CreateDefault(),
            extensionPointRegistry,
            lifetimeManager,
            activationCancellation ?? new HostPluginActivationCancellation(),
            new PostLoginPluginActivator(new PluginServiceRegistrationBridge(extensionPointRegistry, lifetimeManager)),
            activationTracker ?? Substitute.For<IPluginCurrentProcessActivationTracker>(),
            trustVerifier ?? new AllowAllTrustVerifier(),
            licenseRuntimeModeProvider: licenseRuntimeModeProvider ?? new AllowAllLicenseRuntimeModeProvider());
    }

    private static IInstalledPluginStateStore CreateDefaultStateStore()
    {
        var store = Substitute.For<IInstalledPluginStateStore>();
        store.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>([]));
        return store;
    }

    private static IInstalledPluginMetadataCache CreateDefaultMetadataCache()
    {
        var cache = Substitute.For<IInstalledPluginMetadataCache>();
        cache.RebuildFrom(Arg.Any<IReadOnlyList<InstalledPluginState>>()).Returns(Result.Success());
        return cache;
    }

    private static IPluginInstallStagingArea CreateDefaultStagingArea()
    {
        var staging = Substitute.For<IPluginInstallStagingArea>();
        staging.Recover().Returns(Result.Success(0));
        staging.CleanupRuntimeOrphans(Arg.Any<IReadOnlyCollection<string>>()).Returns(Result.Success(0));
        return staging;
    }

    private static string CreateRuntimePackageDirectory()
    {
        var runtimePath = Path.Combine(Path.GetTempPath(), "dobby-plugin-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runtimePath);
        Directory.CreateDirectory(Path.Combine(runtimePath, "content"));
        File.WriteAllText(
            Path.Combine(runtimePath, PluginPackageReader.ManifestFileName),
            PluginPackageTestData.CreateManifestJson());
        var entryAssemblyPath = Path.Combine(runtimePath, PluginPackageTestData.EntryAssembly.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(entryAssemblyPath)!);
        File.WriteAllBytes(
            entryAssemblyPath,
            PluginPackageTestData.EntryAssemblyContent);
        File.WriteAllBytes(
            Path.Combine(runtimePath, PluginPackageTestData.FilePath),
            PluginPackageTestData.FileContent);
        return runtimePath;
    }

    private static string CreateActivatableRuntimePackageDirectory()
    {
        var runtimePath = Path.Combine(Path.GetTempPath(), "dobby-plugin-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runtimePath);
        Directory.CreateDirectory(Path.Combine(runtimePath, "content"));
        var entryAssemblyBytes = File.ReadAllBytes(FindSamplePluginAssembly());
        File.WriteAllText(
            Path.Combine(runtimePath, PluginPackageReader.ManifestFileName),
            PluginPackageTestData.CreateManifestJson(files:
            [
                new PluginPackageFile(
                    PluginPackageTestData.EntryAssembly,
                    PluginPackageTestData.HashHex(entryAssemblyBytes)),
                new PluginPackageFile(
                    PluginPackageTestData.FilePath,
                    PluginPackageTestData.FileHash)
            ]));
        var entryAssemblyPath = Path.Combine(runtimePath, PluginPackageTestData.EntryAssembly.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(entryAssemblyPath)!);
        File.WriteAllBytes(entryAssemblyPath, entryAssemblyBytes);
        File.WriteAllBytes(
            Path.Combine(runtimePath, PluginPackageTestData.FilePath),
            PluginPackageTestData.FileContent);
        return runtimePath;
    }

    private static InstalledPluginState CreateInstalledState(string runtimePath, string packageFingerprint)
    {
        return new InstalledPluginState(
            PluginPackageTestData.PluginId,
            PluginPackageTestData.Version,
            packageFingerprint,
            PluginPackageTestData.DisplayName,
            PluginPackageTestData.Description,
            IsEnabled: true,
            ValidationSummary: "verified",
            RuntimePath: runtimePath,
            StatusSummary: "Enabled",
            InstalledAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static string RuntimePackageFingerprint(string runtimePath)
    {
        var archive = PluginPackageReader.Read(runtimePath);
        archive.IsSuccess.ShouldBeTrue();
        return PluginPackageFingerprint.Compute(archive.Value.Manifest).Fingerprint;
    }

    private static void DeleteRuntimeDirectory(string runtimePath)
    {
        try
        {
            if (Directory.Exists(runtimePath))
            {
                Directory.Delete(runtimePath, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string FindSamplePluginAssembly()
    {
        var root = RepoRoot().FullName;
        var candidates = new[]
        {
            Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", BuildConfiguration(), "net10.0", "Dobby.Sample.Plugin.dll"),
            Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", "Release", "net10.0", "Dobby.Sample.Plugin.dll"),
            Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", "Debug", "net10.0", "Dobby.Sample.Plugin.dll")
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Sample plugin assembly must be built before runtime activation verification.");
    }

    private static string BuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }

    private sealed class AllowAllTrustVerifier : IPluginTrustVerifier
    {
        public PluginTrustVerificationResult Verify(PluginTrustPayload payload) => PluginTrustVerificationResult.Success();
    }

    private sealed class RejectingTrustVerifier(string code, string message) : IPluginTrustVerifier
    {
        public PluginTrustVerificationResult Verify(PluginTrustPayload payload) => PluginTrustVerificationResult.Failure(code, message);
    }

    private sealed class AllowAllLicenseRuntimeModeProvider : ILicenseRuntimeModeProvider
    {
        public Result<PluginRuntimeMode> GetCurrentMode(string pluginId, string packageFingerprint)
        {
            return Result.Success(PluginRuntimeMode.FullAccess);
        }
    }
}

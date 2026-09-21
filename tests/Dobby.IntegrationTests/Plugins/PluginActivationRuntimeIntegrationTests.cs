using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Core;
using Dobby.Contracts.Licensing;
using Dobby.Core.Versioning;
using Dobby.Host.Plugins;
using Dobby.Infrastructure.Persistence;
using Dobby.Licensing;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;
using Dobby.Security.Trust;
using NSubstitute;
using Shouldly;

namespace Dobby.IntegrationTests.Plugins;

public sealed class PluginActivationRuntimeIntegrationTests
{
    [Fact]
    public async Task Host_activation_cascades_dependency_activation_failure_while_independent_plugin_continues()
    {
        using var packages = new RuntimePackageWorkspace();
        packages.Add("core", ModulePayload.InvalidAssembly);
        packages.Add("reports", ModulePayload.InvalidAssembly, [new PluginDependencySpec("core")]);
        packages.Add("standalone", ModulePayload.SampleAssembly);

        await using var coordinator = CreateCoordinator(packages.States);

        var activation = await coordinator.BeginPostLoginActivation(new HostVersion(1, 0, 0), "owner");

        activation.IsSuccess.ShouldBeTrue(activation.IsSuccess ? null : activation.ErrorCode.ToString());
        activation.Value.Completion.ShouldBe(PluginActivationCompletion.Completed);
        var outcomes = activation.Value.Run.Outcomes.ToDictionary(outcome => outcome.PluginId, StringComparer.Ordinal);
        outcomes["core"].Status.ShouldBe(PluginActivationStatus.Faulted);
        outcomes["reports"].Status.ShouldBe(PluginActivationStatus.Skipped);
        outcomes["reports"].Reason.ShouldBe("dependency-activation-failed: core");
        outcomes["standalone"].Status.ShouldBe(PluginActivationStatus.Active);
        activation.Value.Run.ActivatedPluginIds.ShouldBe(["standalone"]);
    }

    [Fact]
    public async Task Host_activation_static_dependency_skips_missing_and_circular_plugins()
    {
        using var packages = new RuntimePackageWorkspace();
        packages.Add("control", ModulePayload.SampleAssembly);
        packages.Add("missing-consumer", ModulePayload.InvalidAssembly, [new PluginDependencySpec("missing-core")]);
        packages.Add("cycle-a", ModulePayload.InvalidAssembly, [new PluginDependencySpec("cycle-b")]);
        packages.Add("cycle-b", ModulePayload.InvalidAssembly, [new PluginDependencySpec("cycle-a")]);

        await using var coordinator = CreateCoordinator(packages.States);

        var activation = await coordinator.BeginPostLoginActivation(new HostVersion(1, 0, 0), "owner");

        activation.IsSuccess.ShouldBeTrue(activation.IsSuccess ? null : activation.ErrorCode.ToString());
        activation.Value.Completion.ShouldBe(PluginActivationCompletion.Completed);
        var outcomes = activation.Value.Run.Outcomes.ToDictionary(outcome => outcome.PluginId, StringComparer.Ordinal);
        outcomes["control"].Status.ShouldBe(PluginActivationStatus.Active);
        outcomes["missing-consumer"].Status.ShouldBe(PluginActivationStatus.Skipped);
        outcomes["missing-consumer"].Reason.ShouldBe("missing-dependency: missing-core");
        outcomes["cycle-a"].Status.ShouldBe(PluginActivationStatus.Skipped);
        outcomes["cycle-a"].Reason.ShouldBe("circular-dependency: cycle-a,cycle-b");
        outcomes["cycle-b"].Status.ShouldBe(PluginActivationStatus.Skipped);
        outcomes["cycle-b"].Reason.ShouldBe("circular-dependency: cycle-a,cycle-b");
        activation.Value.Run.ActivatedPluginIds.ShouldBe(["control"]);
    }

    [Fact]
    public async Task Host_activation_cancellation_reset_allows_next_runtime_activation_to_complete()
    {
        using var packages = new RuntimePackageWorkspace();
        packages.Add("retryable", ModulePayload.SampleAssembly);
        var cancellation = new HostPluginActivationCancellation();
        await using var cancelledCoordinator = CreateCoordinator(
            packages.States,
            activationCancellation: cancellation);

        cancellation.CancelPostLoginActivation();
        var cancelled = await cancelledCoordinator.BeginPostLoginActivation(new HostVersion(1, 0, 0), "owner");

        cancelled.IsSuccess.ShouldBeTrue(cancelled.IsSuccess ? null : cancelled.ErrorCode.ToString());
        cancelled.Value.Completion.ShouldBe(PluginActivationCompletion.Cancelled);
        cancelled.Value.Run.Outcomes.ShouldBeEmpty();

        var reset = cancelledCoordinator.ApplyPostLoginMigrations();
        reset.IsSuccess.ShouldBeTrue(reset.IsSuccess ? null : reset.Error.Message);
        cancellation.Token.IsCancellationRequested.ShouldBeFalse();

        await using var retryCoordinator = CreateCoordinator(packages.States);
        var retried = await retryCoordinator.BeginPostLoginActivation(new HostVersion(1, 0, 0), "owner");

        retried.IsSuccess.ShouldBeTrue(retried.IsSuccess ? null : retried.ErrorCode.ToString());
        retried.Value.Completion.ShouldBe(PluginActivationCompletion.Completed);
        var outcome = retried.Value.Run.Outcomes.Single();
        outcome.PluginId.ShouldBe("retryable");
        outcome.Status.ShouldBe(PluginActivationStatus.Active);
        retried.Value.Run.ActivatedPluginIds.ShouldBe(["retryable"]);
    }

    [Fact]
    public async Task Host_activation_seeds_manifest_operation_targets_with_plugin_origin()
    {
        using var packages = new RuntimePackageWorkspace();
        packages.Add(
            "ops.plugin",
            ModulePayload.SampleAssembly,
            operationTargetName: "sync",
            operationTargetVerbs: ["view", "execute"],
            operationTargetLabelKey: "ops.sync.label");
        var operationTargets = OperationTargetRegistry.CreateDefault();
        await using var coordinator = CreateCoordinator(
            packages.States,
            operationTargetRegistry: operationTargets);

        var activation = await coordinator.BeginPostLoginActivation(new HostVersion(1, 0, 0), "owner");

        activation.IsSuccess.ShouldBeTrue(activation.IsSuccess ? null : activation.ErrorCode.ToString());
        activation.Value.Completion.ShouldBe(PluginActivationCompletion.Completed);
        activation.Value.Run.Outcomes.Single().Status.ShouldBe(PluginActivationStatus.Active);
        var target = operationTargets.Get("ops.plugin.sync");
        target.IsSuccess.ShouldBeTrue(target.IsSuccess ? null : target.ErrorCode.ToString());
        target.Value.Origin.ShouldBe(OperationTargetOrigin.Plugin);
        target.Value.SupportedVerbs.ShouldBe([OperationVerb.Execute, OperationVerb.View]);
        target.Value.LabelKey.ShouldBe("ops.sync.label");
    }

    [Fact]
    public async Task Host_activation_persists_safe_fault_status_summaries_for_manager_ui()
    {
        using var packages = new RuntimePackageWorkspace();
        packages.Add("core", ModulePayload.InvalidAssembly);
        packages.Add("reports", ModulePayload.InvalidAssembly, [new PluginDependencySpec("core")]);
        packages.Add("standalone", ModulePayload.SampleAssembly);
        var stateStore = new InMemoryInstalledPluginStateStore(packages.States);
        var metadataCache = new InMemoryInstalledPluginMetadataCache(packages.States);
        var licenseModes = new SelectiveLicenseRuntimeModeProvider("standalone");
        await using var coordinator = CreateCoordinator(
            stateStore.Snapshot().Value,
            stateStore: stateStore,
            metadataCache: metadataCache,
            activationStatusPersistence: new InMemoryActivationStatusPersistence(stateStore, metadataCache),
            licenseRuntimeModeProvider: licenseModes);

        var activation = await coordinator.BeginPostLoginActivation(new HostVersion(1, 0, 0), "owner");

        activation.IsSuccess.ShouldBeTrue(activation.IsSuccess ? null : activation.ErrorCode.ToString());
        var persistedStates = stateStore.Snapshot().Value.ToDictionary(item => item.PluginId, StringComparer.Ordinal);
        var metadata = metadataCache.SnapshotItems().ToDictionary(item => item.PluginId, StringComparer.Ordinal);

        persistedStates["core"].StatusSummary.ShouldBe("Faulted - Plugin activation failed");
        persistedStates["reports"].StatusSummary.ShouldBe("Skipped - Dependency activation failed");
        persistedStates["standalone"].StatusSummary.ShouldBe("Rejected - License denied");
        metadata["core"].StatusSummary.ShouldBe("Faulted - Plugin activation failed");
        metadata["reports"].StatusSummary.ShouldBe("Skipped - Dependency activation failed");
        metadata["standalone"].StatusSummary.ShouldBe("Rejected - License denied");
    }

    // ADR-0009 §3 and §6: a below-minimum dependency version must reach the manager UI as an
    // actionable non-disclosing summary, and the activation skip must cascade transitively so no
    // plugin runs with an unsatisfied `dependsOn` chain.
    [Fact]
    public async Task Host_activation_reports_version_skip_summary_and_cascades_skips_transitively()
    {
        using var packages = new RuntimePackageWorkspace();
        packages.Add("core", ModulePayload.InvalidAssembly);
        packages.Add("mid", ModulePayload.SampleAssembly, [new PluginDependencySpec("core")]);
        packages.Add("leaf", ModulePayload.SampleAssembly, [new PluginDependencySpec("mid")]);
        packages.Add("version-consumer", ModulePayload.SampleAssembly, [new PluginDependencySpec("core", "9.0.0")]);
        var stateStore = new InMemoryInstalledPluginStateStore(packages.States);
        var metadataCache = new InMemoryInstalledPluginMetadataCache(packages.States);
        await using var coordinator = CreateCoordinator(
            stateStore.Snapshot().Value,
            stateStore: stateStore,
            metadataCache: metadataCache,
            activationStatusPersistence: new InMemoryActivationStatusPersistence(stateStore, metadataCache));

        var activation = await coordinator.BeginPostLoginActivation(new HostVersion(1, 0, 0), "owner");

        activation.IsSuccess.ShouldBeTrue(activation.IsSuccess ? null : activation.ErrorCode.ToString());
        var outcomes = activation.Value.Run.Outcomes.ToDictionary(outcome => outcome.PluginId, StringComparer.Ordinal);
        outcomes["core"].Status.ShouldBe(PluginActivationStatus.Faulted);
        outcomes["mid"].Reason.ShouldBe("dependency-activation-failed: core");
        outcomes["leaf"].Status.ShouldBe(PluginActivationStatus.Skipped);
        outcomes["leaf"].Reason.ShouldBe("dependency-activation-failed: mid");
        outcomes["version-consumer"].Reason.ShouldBe("dependency-version-below-min: core:1.0.0:9.0.0");
        activation.Value.Run.ActivatedPluginIds.ShouldBeEmpty();

        var persistedStates = stateStore.Snapshot().Value.ToDictionary(item => item.PluginId, StringComparer.Ordinal);
        persistedStates["leaf"].StatusSummary.ShouldBe("Skipped - Dependency activation failed");
        persistedStates["version-consumer"].StatusSummary.ShouldBe("Skipped - Dependency version too low");
    }

    private static HostPluginRuntimeCoordinator CreateCoordinator(
        IReadOnlyList<InstalledPluginState> states,
        OperationTargetRegistry? operationTargetRegistry = null,
        HostPluginActivationCancellation? activationCancellation = null,
        IInstalledPluginStateStore? stateStore = null,
        IInstalledPluginMetadataCache? metadataCache = null,
        IHostPluginActivationStatusPersistence? activationStatusPersistence = null,
        ILicenseRuntimeModeProvider? licenseRuntimeModeProvider = null)
    {
        if (stateStore is null)
        {
            stateStore = Substitute.For<IInstalledPluginStateStore>();
            stateStore.Snapshot().Returns(Result.Success(states));
            stateStore.Upsert(Arg.Any<InstalledPluginState>()).Returns(Result.Success());
        }

        if (metadataCache is null)
        {
            metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
            metadataCache.RebuildFrom(Arg.Any<IReadOnlyList<InstalledPluginState>>()).Returns(Result.Success());
        }
        var stagingArea = Substitute.For<IPluginInstallStagingArea>();
        stagingArea.Recover().Returns(Result.Success(0));
        stagingArea.CleanupRuntimeOrphans(Arg.Any<IReadOnlyCollection<string>>()).Returns(Result.Success(0));
        var migrator = Substitute.For<IHostPersistenceMigrator>();
        migrator.ApplyPostLoginMigrations(Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(PersistenceResult.Success());

        var extensionRegistry = new PluginExtensionPointRegistry();
        var lifetimes = new PluginLifetimeManager();
        return new HostPluginRuntimeCoordinator(
            migrator,
            stateStore,
            metadataCache,
            activationStatusPersistence ?? CreateSuccessfulActivationStatusPersistence(),
            stagingArea,
            Substitute.For<IPluginDataRetentionStore>(),
            operationTargetRegistry ?? OperationTargetRegistry.CreateDefault(),
            extensionRegistry,
            lifetimes,
            activationCancellation ?? new HostPluginActivationCancellation(),
            new PostLoginPluginActivator(new PluginServiceRegistrationBridge(extensionRegistry, lifetimes)),
            Substitute.For<IPluginCurrentProcessActivationTracker>(),
            new AllowAllTrustVerifier(),
            licenseRuntimeModeProvider: licenseRuntimeModeProvider);
    }

    private static IHostPluginActivationStatusPersistence CreateSuccessfulActivationStatusPersistence()
    {
        var persistence = Substitute.For<IHostPluginActivationStatusPersistence>();
        persistence
            .Persist(Arg.Any<IReadOnlyList<InstalledPluginState>>(), Arg.Any<IReadOnlyList<InstalledPluginState>>())
            .Returns(Result.Success());
        return persistence;
    }

    private enum ModulePayload
    {
        SampleAssembly,
        InvalidAssembly
    }

    private sealed record PluginDependencySpec(string PluginId, string MinVersion = RuntimePackageWorkspace.Version);

    private sealed class RuntimePackageWorkspace : IDisposable
    {
        public const string Version = "1.0.0";

        private const string EntryAssembly = "bin/plugins/plugin-entry.dll";
        private static readonly byte[] InvalidAssemblyBytes = Encoding.UTF8.GetBytes("not a managed plugin module");
        private readonly string root;
        private readonly List<InstalledPluginState> states = [];

        public RuntimePackageWorkspace()
        {
            root = Path.Combine(
                Path.GetTempPath(),
                "dobby-plugin-activation-runtime-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public IReadOnlyList<InstalledPluginState> States => states.ToArray();

        public void Add(
            string pluginId,
            ModulePayload modulePayload,
            IReadOnlyList<PluginDependencySpec>? dependencies = null,
            string operationTargetName = "greet",
            IReadOnlyList<string>? operationTargetVerbs = null,
            string? operationTargetLabelKey = null)
        {
            dependencies ??= [];
            operationTargetVerbs ??= ["execute"];
            operationTargetLabelKey ??= $"{pluginId}.{operationTargetName}.label";
            var runtimePath = Path.Combine(root, pluginId);
            Directory.CreateDirectory(runtimePath);
            var entryBytes = modulePayload == ModulePayload.SampleAssembly
                ? File.ReadAllBytes(FindSamplePluginAssembly())
                : InvalidAssemblyBytes;
            var entryPath = Path.Combine(runtimePath, EntryAssembly.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
            File.WriteAllBytes(entryPath, entryBytes);
            var manifestJson = CreateManifestJson(
                pluginId,
                dependencies,
                $"{pluginId}.{operationTargetName}",
                operationTargetVerbs,
                operationTargetLabelKey,
                entryBytes);
            File.WriteAllText(Path.Combine(runtimePath, PluginPackageReader.ManifestFileName), manifestJson, Encoding.UTF8);

            var archive = PluginPackageReader.Read(runtimePath);
            archive.IsSuccess.ShouldBeTrue(archive.IsSuccess ? null : archive.ErrorCode.ToString());
            var fingerprint = PluginPackageFingerprint.Compute(archive.Value.Manifest).Fingerprint;
            states.Add(new InstalledPluginState(
                pluginId,
                Version,
                fingerprint,
                $"{pluginId} display",
                $"{pluginId} description",
                IsEnabled: true,
                ValidationSummary: "verified",
                RuntimePath: runtimePath,
                StatusSummary: "Enabled",
                InstalledAtUtc: DateTimeOffset.UtcNow,
                UpdatedAtUtc: DateTimeOffset.UtcNow));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        private static string CreateManifestJson(
            string pluginId,
            IReadOnlyList<PluginDependencySpec> dependencies,
            string operationTargetId,
            IReadOnlyList<string> operationTargetVerbs,
            string operationTargetLabelKey,
            byte[] entryBytes)
        {
            var manifest = new Dictionary<string, object?>
            {
                ["schemaVersion"] = 2,
                ["purpose"] = "dobby.plugin-package",
                ["pluginId"] = pluginId,
                ["version"] = Version,
                ["vendorId"] = "dobby.vendor",
                ["hostApiVersion"] = "2.0",
                ["minHostVersion"] = "1.0.0",
                ["entryAssembly"] = EntryAssembly,
                ["displayName"] = $"{pluginId} display",
                ["description"] = $"{pluginId} description",
                ["dependsOn"] = dependencies
                    .Select(dependency => new Dictionary<string, object?>
                    {
                        ["pluginId"] = dependency.PluginId,
                        ["minVersion"] = dependency.MinVersion
                    })
                    .ToArray(),
                ["capabilities"] = Array.Empty<Dictionary<string, object?>>(),
                ["operationTargets"] = new Dictionary<string, object?>[]
                {
                    new Dictionary<string, object?>
                    {
                        ["id"] = operationTargetId,
                        ["verbs"] = operationTargetVerbs.ToArray(),
                        ["labelKey"] = operationTargetLabelKey
                    }
                },
                ["extensionPoints"] = Array.Empty<Dictionary<string, object?>>(),
                ["files"] = new Dictionary<string, object?>[]
                {
                    new Dictionary<string, object?>
                    {
                        ["path"] = EntryAssembly,
                        ["sha256"] = Sha256(entryBytes)
                    }
                },
                ["signature"] = new Dictionary<string, object?>
                {
                    ["purpose"] = "dobby.plugin-package",
                    ["algorithm"] = "ECDSA-P256-SHA256",
                    ["format"] = "IeeeP1363FixedFieldConcatenation",
                    ["keyId"] = "plugin-prod-2026-01",
                    ["value"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
                }
            };

            return JsonSerializer.Serialize(manifest);
        }

        private static string FindSamplePluginAssembly()
        {
            var root = FindRepositoryRoot();
            var candidates = new[]
            {
                Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", BuildConfiguration(), "net10.0", "Dobby.Sample.Plugin.dll"),
                Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", "Release", "net10.0", "Dobby.Sample.Plugin.dll"),
                Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", "Debug", "net10.0", "Dobby.Sample.Plugin.dll")
            };

            var match = candidates.FirstOrDefault(File.Exists);
            return match ?? throw new InvalidOperationException("Sample plugin assembly was not built.");
        }

        private static string FindRepositoryRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Dobby.sln")))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException("Repository root was not found.");
        }

        private static string BuildConfiguration()
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }

        private static string Sha256(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
    }

    private sealed class AllowAllTrustVerifier : IPluginTrustVerifier
    {
        public PluginTrustVerificationResult Verify(PluginTrustPayload payload) => PluginTrustVerificationResult.Success();
    }

    private sealed class SelectiveLicenseRuntimeModeProvider(params string[] deniedPluginIds) : ILicenseRuntimeModeProvider
    {
        private readonly HashSet<string> denied = deniedPluginIds.ToHashSet(StringComparer.Ordinal);

        public Result<PluginRuntimeMode> GetCurrentMode(string pluginId, string fingerprint)
        {
            return Result.Success(
                denied.Contains(pluginId)
                    ? PluginRuntimeMode.Denied
                    : PluginRuntimeMode.FullAccess);
        }
    }

    private sealed class InMemoryInstalledPluginStateStore(IReadOnlyList<InstalledPluginState> states) : IInstalledPluginStateStore
    {
        private readonly Dictionary<string, InstalledPluginState> byPluginId = states.ToDictionary(
            state => state.PluginId,
            StringComparer.Ordinal);

        public Result Upsert(InstalledPluginState state)
        {
            byPluginId[state.PluginId] = state;
            return Result.Success();
        }

        public Result<InstalledPluginState?> Get(string pluginId)
        {
            return Result.Success(byPluginId.TryGetValue(pluginId, out var state) ? state : null);
        }

        public Result<IReadOnlyList<InstalledPluginState>> Snapshot()
        {
            return Result.Success<IReadOnlyList<InstalledPluginState>>(byPluginId.Values.OrderBy(state => state.PluginId, StringComparer.Ordinal).ToArray());
        }

        public Result SetEnabled(string pluginId, bool isEnabled, string statusSummary)
        {
            if (!byPluginId.TryGetValue(pluginId, out var state))
            {
                return Result.Failure(ErrorCode.NotFound);
            }

            byPluginId[pluginId] = state with
            {
                IsEnabled = isEnabled,
                StatusSummary = statusSummary,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            return Result.Success();
        }

        public Result Remove(string pluginId)
        {
            byPluginId.Remove(pluginId);
            return Result.Success();
        }
    }

    private sealed class InMemoryInstalledPluginMetadataCache(IReadOnlyList<InstalledPluginState> states) : IInstalledPluginMetadataCache
    {
        private readonly Dictionary<string, InstalledPluginMetadata> byPluginId = states.ToDictionary(
            state => state.PluginId,
            ToMetadata,
            StringComparer.Ordinal);

        public Result Upsert(InstalledPluginMetadata metadata)
        {
            byPluginId[metadata.PluginId] = metadata;
            return Result.Success();
        }

        public Result<InstalledPluginMetadata?> Get(string pluginId)
        {
            return Result.Success(byPluginId.TryGetValue(pluginId, out var metadata) ? metadata : null);
        }

        public Result<IReadOnlyList<InstalledPluginMetadata>> Snapshot()
        {
            return Result.Success<IReadOnlyList<InstalledPluginMetadata>>(SnapshotItems());
        }

        public IReadOnlyList<InstalledPluginMetadata> SnapshotItems()
        {
            return byPluginId.Values.OrderBy(item => item.PluginId, StringComparer.Ordinal).ToArray();
        }

        public Result RebuildFrom(IReadOnlyList<InstalledPluginState> installedStates)
        {
            byPluginId.Clear();
            foreach (var state in installedStates)
            {
                byPluginId[state.PluginId] = ToMetadata(state);
            }

            return Result.Success();
        }

        public Result Remove(string pluginId)
        {
            byPluginId.Remove(pluginId);
            return Result.Success();
        }

        private static InstalledPluginMetadata ToMetadata(InstalledPluginState state)
        {
            return new InstalledPluginMetadata(
                state.PluginId,
                state.DisplayName,
                state.Version,
                state.Description,
                state.PackageFingerprint,
                state.IsEnabled,
                state.StatusSummary,
                state.InstalledAtUtc,
                state.UpdatedAtUtc,
                state.IsPendingUninstall,
                state.RestartRequired);
        }
    }

    private sealed class InMemoryActivationStatusPersistence(
        IInstalledPluginStateStore stateStore,
        IInstalledPluginMetadataCache metadataCache) : IHostPluginActivationStatusPersistence
    {
        public Result Persist(
            IReadOnlyList<InstalledPluginState> installedStates,
            IReadOnlyList<InstalledPluginState> changedStates)
        {
            foreach (var state in changedStates)
            {
                var upsert = stateStore.Upsert(state);
                if (!upsert.IsSuccess)
                {
                    return upsert;
                }
            }

            return metadataCache.RebuildFrom(installedStates);
        }
    }
}

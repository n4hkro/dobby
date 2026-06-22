using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Host.Plugins;
using Dobby.Infrastructure.Persistence;
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

    private static HostPluginRuntimeCoordinator CreateCoordinator(
        IReadOnlyList<InstalledPluginState> states,
        OperationTargetRegistry? operationTargetRegistry = null,
        HostPluginActivationCancellation? activationCancellation = null)
    {
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        stateStore.Snapshot().Returns(Result.Success(states));
        var metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
        metadataCache.RebuildFrom(Arg.Any<IReadOnlyList<InstalledPluginState>>()).Returns(Result.Success());
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
            stagingArea,
            Substitute.For<IPluginDataRetentionStore>(),
            operationTargetRegistry ?? OperationTargetRegistry.CreateDefault(),
            extensionRegistry,
            lifetimes,
            activationCancellation ?? new HostPluginActivationCancellation(),
            new PostLoginPluginActivator(new PluginServiceRegistrationBridge(extensionRegistry, lifetimes)),
            Substitute.For<IPluginCurrentProcessActivationTracker>(),
            new AllowAllTrustVerifier());
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
                ["pluginId"] = pluginId,
                ["version"] = Version,
                ["vendorId"] = "dobby.vendor",
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
                    ["algorithm"] = "ECDSA-P256-SHA256",
                    ["keyVersion"] = 1,
                    ["value"] = "AQ"
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
}

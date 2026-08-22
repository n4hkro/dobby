using System.Reflection;
using Dobby.Audit;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginStorage;
using Dobby.Core;
using Dobby.Diagnostics;
using Dobby.Host;
using Dobby.Host.Plugins;
using Dobby.Host.Printing;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Licensing;
using Dobby.Shell.PluginPages;
using Shouldly;

namespace Dobby.IntegrationTests.Licensing;

public sealed class ReadOnlyExpiredEnforcementIntegrationTests
{
    [Fact]
    public async Task ReadOnlyExpired_blocks_all_mutation_surfaces()
    {
        using var fixture = PluginStoragePersistenceFixture.Create();
        var fullAccessGate = new HostRuntimeModeMutationGate(
            _ => Result.Success(PluginRuntimeMode.FullAccess));
        var readOnlyGate = new HostRuntimeModeMutationGate(
            _ => Result.Success(PluginRuntimeMode.ReadOnlyExpired));
        var writableStorage = new HostPluginStorage(
            "plugin.sample",
            fixture.Executor,
            fixture.TransactionScope,
            () => "user-a",
            fullAccessGate);
        var readOnlyStorage = new HostPluginStorage(
            "plugin.sample",
            fixture.Executor,
            fixture.TransactionScope,
            () => "user-a",
            readOnlyGate);
        var key = PluginStorageKey.Create("settings/theme").Value;
        var original = PluginStorageValue.FromUtf8Json("""{"theme":"blue"}""");
        var replacement = PluginStorageValue.FromUtf8Json("""{"theme":"red"}""");

        (await writableStorage.SharedDomainStorage.SetAsync(key, original)).IsSuccess.ShouldBeTrue();

        var set = await readOnlyStorage.SharedDomainStorage.SetAsync(key, replacement);
        var delete = await readOnlyStorage.SharedDomainStorage.DeleteAsync(key);
        var persisted = await readOnlyStorage.SharedDomainStorage.GetAsync(key);

        readOnlyGate.EnsurePluginMutationAllowed("plugin.sample").ErrorCode.ShouldBe(ErrorCode.LicenseReadOnly);
        set.ErrorCode.ShouldBe(ErrorCode.LicenseReadOnly);
        delete.ErrorCode.ShouldBe(ErrorCode.LicenseReadOnly);
        persisted.IsSuccess.ShouldBeTrue();
        persisted.Value.ShouldNotBeNull();
        persisted.Value.Payload.ToArray().ShouldBe(original.Payload.ToArray());
    }

    [Fact]
    public void Runtime_action_command_owners_declare_their_central_policy_surfaces()
    {
        AssertOwner<HostPluginStorageCompartment>(PluginRuntimeAction.StorageRead, PluginRuntimeAction.StorageWrite);
        AssertOwner<HostPluginPackageInstallCoordinator>(PluginRuntimeAction.InitialPackageInstall, PluginRuntimeAction.PackageUpdate);
        AssertOwner<HostPluginLifecycleCoordinator>(PluginRuntimeAction.EnableDisableUninstall);
        AssertOwner<HostPrintOutputService>(PluginRuntimeAction.PhysicalPrint, PluginRuntimeAction.VirtualOrFileOutput);
        AssertOwner<PluginPageRenderRegistry>(PluginRuntimeAction.PluginAction);
        AssertOwner<ShellDiagnosticsExportCoordinator>(PluginRuntimeAction.HostDiagnostics);

        var futurePolicyOwnerNames = new[] { "Background", "DomainImport", "DomainExport", "Report", "FileOutput" };
        var unowned = new[] { typeof(HostPluginStorageCompartment).Assembly, typeof(PluginPageRenderRegistry).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract && type.Name.Contains("Plugin", StringComparison.Ordinal))
            .Where(type => futurePolicyOwnerNames.Any(marker => type.Name.Contains(marker, StringComparison.Ordinal)))
            .Where(type => type.GetCustomAttribute<PluginRuntimeActionOwnerAttribute>() is null)
            .Select(type => type.FullName)
            .ToArray();
        unowned.ShouldBeEmpty("future plugin background/import/export/report/file-output owners must declare their runtime action policy.");
    }

    [Theory]
    [InlineData(LicenseLifecycleState.Pending)]
    [InlineData(LicenseLifecycleState.Active)]
    [InlineData(LicenseLifecycleState.Grace)]
    [InlineData(LicenseLifecycleState.ReadOnlyExpired)]
    [InlineData(LicenseLifecycleState.Denied)]
    public void Minimal_host_diagnostics_export_is_independent_of_plugin_license_lifecycle(
        LicenseLifecycleState lifecycle)
    {
        var mode = LicenseRuntimeModeMapper.Map(lifecycle);
        PluginRuntimeActionPolicy.Evaluate(mode, PluginRuntimeAction.HostDiagnostics)
            .ShouldBe(PluginRuntimeActionDecision.Allowed);

        var root = Path.Combine(Path.GetTempPath(), "dobby-host-diagnostics-license-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "logs");
        var output = Path.Combine(root, "support.zip");
        try
        {
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "dobby.log"), "safe=value");
            var result = new ShellDiagnosticsExportCoordinator(
                new DiagnosticsExportPackageBuilder(),
                new InMemoryAuditTrail(),
                TimeProvider.System).Export(
                    "owner-1",
                    source,
                    output,
                    CorrelationId.New());

            result.IsSuccess.ShouldBeTrue();
            File.Exists(output).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void AssertOwner<TOwner>(params PluginRuntimeAction[] expected)
    {
        var attribute = typeof(TOwner).GetCustomAttribute<PluginRuntimeActionOwnerAttribute>();
        attribute.ShouldNotBeNull();
        attribute.Actions.Order().ShouldBe(expected.Order());
    }

    private sealed class PluginStoragePersistenceFixture : IDisposable
    {
        private PluginStoragePersistenceFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            var migrator = new HostPersistenceMigrator(factory, probe);
            migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
            TransactionScope = new HostPersistenceTransactionScope(factory);
        }

        public string Root { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public HostPersistenceTransactionScope TransactionScope { get; }

        public static PluginStoragePersistenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-readonly-storage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new PluginStoragePersistenceFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

}

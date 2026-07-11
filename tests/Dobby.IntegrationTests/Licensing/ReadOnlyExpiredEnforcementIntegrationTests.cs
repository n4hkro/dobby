using System.Text.RegularExpressions;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginStorage;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
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
    public void Readonly_expired_matrix_has_gate_tests_or_explicit_absent_surface_proof()
    {
        var root = RepoRoot();
        var sourceFiles = SourceFiles(root).ToArray();
        var actionPipeline = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Shell",
            "PluginPages",
            "PluginPageRenderRegistry.cs"));
        var lifecycleCoordinator = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Host",
            "Plugins",
            "HostPluginLifecycleCoordinator.cs"));
        var printOutputService = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Host",
            "Printing",
            "HostPrintOutputService.cs"));
        var diagnosticsExportCoordinator = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Host",
            "ShellDiagnosticsExportCoordinator.cs"));
        var loginRuntimeFactory = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Host",
            "Login",
            "HostLoginRuntimeFactory.cs"));
        var loginRuntime = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Host",
            "Login",
            "HostLoginRuntime.cs"));

        actionPipeline.ShouldContain("actionExecutionGate(model.Context.PluginId)");
        lifecycleCoordinator.ShouldContain("EnsurePluginMutationAllowed(pluginId)");
        printOutputService.ShouldContain("runtimeModeProvider.GetCurrentMode(pluginId, packageFingerprint)");
        printOutputService.ShouldContain("printOutputCoordinator.ExecuteAsync");
        diagnosticsExportCoordinator.ShouldContain("outputGate?.Invoke()");
        diagnosticsExportCoordinator.ShouldContain("AuditResult.Denied");
        loginRuntimeFactory.ShouldContain("EnsureDiagnosticsExportAllowed(licenseRuntimeModeProvider, installedPluginStateStore)");
        loginRuntimeFactory.ShouldContain("PluginRuntimeMode.ReadOnlyExpired");
        loginRuntime.ShouldContain("ExecutePluginPrintAsync");
        loginRuntime.ShouldContain("PrintOutputService.ExecuteAsync");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*PluginBackground(Task|Operation)\w*(Starter|Coordinator|Runner)\w*")
            .ShouldBeEmpty("plugin background task start has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*Plugin(Domain)?Import\w*(Coordinator|Service|Writer)\w*")
            .ShouldBeEmpty("plugin/domain import has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*Plugin(Domain)?Export\w*(Coordinator|Service|Writer)\w*")
            .ShouldBeEmpty("plugin/domain export has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*PluginReport\w*(Coordinator|Service|Writer)\w*")
            .ShouldBeEmpty("plugin/domain report output has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
        ConcreteSourceOwners(sourceFiles, @"\bclass\s+\w*PluginFile(Output|Export)\w*(Coordinator|Service|Writer)\w*")
            .ShouldBeEmpty("plugin/domain file output has no concrete owner yet; add a ReadOnlyExpired gate test when one appears.");
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

    private static IEnumerable<string> ConcreteSourceOwners(IEnumerable<FileInfo> sourceFiles, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        return sourceFiles
            .Where(file => regex.IsMatch(File.ReadAllText(file.FullName)))
            .Select(file => file.FullName)
            .ToArray();
    }

    private static IEnumerable<FileInfo> SourceFiles(DirectoryInfo root)
    {
        return root.GetDirectories("src").Single()
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file =>
                !file.FullName.Contains($"{Path.DirectorySeparatorChar}Dobby.Contracts{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !file.FullName.Contains($"{Path.DirectorySeparatorChar}Dobby.SDK{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static DirectoryInfo RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dobby.sln")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

using Dobby.Host;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.CurrentContext;
using Microsoft.Data.Sqlite;
using Shouldly;

namespace Dobby.IntegrationTests.Licensing;

public sealed class CustomerDeploymentContextIntegrationTests
{
    [Fact]
    public void Provisioned_context_is_single_security_owned_context()
    {
        using var fixture = CurrentContextPersistenceFixture.Create();
        var store = new HostCustomerDeploymentContextStore(fixture.Executor, fixture.TransactionScope);
        var service = new CurrentCustomerDeploymentContextService(store);
        var provisioned = service.ProvisionContext("customer-a", "deployment-a");
        var current = service.GetCurrentContext();

        provisioned.IsSuccess.ShouldBeTrue();
        current.IsSuccess.ShouldBeTrue();
        current.Context.ShouldBe(new CurrentCustomerDeploymentContext("customer-a", "deployment-a"));
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM CustomerDeploymentContext;")
            .ShouldBe(1);
    }

    [Fact]
    public void Host_internal_provisioning_path_uses_authorization_and_audit_boundary()
    {
        var root = RepoRoot();
        var factorySource = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Host",
            "Login",
            "HostLoginRuntimeFactory.cs"));
        var coordinatorSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Host",
            "HostCustomerDeploymentContextProvisioningCoordinator.cs"));
        var runtimeSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Host",
            "Login",
            "HostLoginRuntime.cs"));

        factorySource.ShouldContain("new HostCustomerDeploymentContextProvisioningCoordinator");
        runtimeSource.ShouldContain("ProvisionCustomerDeploymentContext");
        runtimeSource.ShouldContain("CustomerDeploymentContextProvisioningCoordinator.Provision");
        coordinatorSource.ShouldContain("OperationAuthorizationPolicy");
        coordinatorSource.ShouldContain("AuditEventName.CustomerDeploymentContextProvision");
        coordinatorSource.ShouldContain("AuditEventName.PermissionDenied");
        coordinatorSource.ShouldContain("customerIdHash");
        coordinatorSource.ShouldContain("deploymentIdHash");
    }

    [Fact]
    public void Host_store_materializes_exactly_one_default_context_for_clean_install()
    {
        using var fixture = CurrentContextPersistenceFixture.Create();
        var store = new HostCustomerDeploymentContextStore(fixture.Executor, fixture.TransactionScope);
        var service = new CurrentCustomerDeploymentContextService(store);

        var result = service.EnsureDefaultContext();
        var second = service.EnsureDefaultContext();

        result.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM CustomerDeploymentContext;")
            .ShouldBe(1);
        fixture.Executor.Query<string>("SELECT CustomerId || ':' || DeploymentId FROM CustomerDeploymentContext;")
            .ShouldBe(["default:default"]);
    }

    [Fact]
    public void Host_store_lookup_does_not_seed_context_for_import_path()
    {
        using var fixture = CurrentContextPersistenceFixture.Create();
        var store = new HostCustomerDeploymentContextStore(fixture.Executor, fixture.TransactionScope);
        var service = new CurrentCustomerDeploymentContextService(store);

        var result = service.GetCurrentContext();

        result.IsSuccess.ShouldBeFalse();
        result.SupportCode.ShouldBe("missing-current-context");
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM CustomerDeploymentContext;")
            .ShouldBe(0);
    }

    [Fact]
    public void Provision_context_rolls_back_original_context_when_replacement_insert_fails()
    {
        using var fixture = CurrentContextPersistenceFixture.Create();
        var store = new HostCustomerDeploymentContextStore(fixture.Executor, fixture.TransactionScope);
        var service = new CurrentCustomerDeploymentContextService(store);
        service.EnsureDefaultContext().IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            """
            CREATE TRIGGER RejectAdminProvisioning
            BEFORE INSERT ON CustomerDeploymentContext
            WHEN NEW.Source = 'admin-operator-provisioning'
            BEGIN
                SELECT RAISE(ABORT, 'simulated-context-provision-failure');
            END;
            """);

        var exception = Should.Throw<SqliteException>(() =>
            service.ProvisionContext("customer-a", "deployment-a"));
        var current = service.GetCurrentContext();

        exception.Message.ShouldContain("simulated-context-provision-failure");
        current.IsSuccess.ShouldBeTrue();
        current.Context.ShouldBe(new CurrentCustomerDeploymentContext("default", "default"));
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM CustomerDeploymentContext;")
            .ShouldBe(1);
    }

    [Fact]
    public void Raw_context_identifiers_are_absent_from_plugin_and_user_surfaces()
    {
        var root = RepoRoot();
        var directories = new[]
        {
            Path.Combine(root.FullName, "src", "Dobby.Contracts"),
            Path.Combine(root.FullName, "src", "Dobby.SDK"),
            Path.Combine(root.FullName, "src", "Dobby.PluginFramework"),
            Path.Combine(root.FullName, "src", "Dobby.Shell")
        };
        var files = directories
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Concat([
                Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"),
                Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"),
                Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"),
                Path.Combine(root.FullName, "src", "Dobby.Host", "Plugins", "LicenseImportShellAction.cs")
            ]);

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            source.Contains("customerId", StringComparison.OrdinalIgnoreCase)
                .ShouldBeFalse($"Raw customer identifiers must not be visible in {file}.");
            source.Contains("deploymentId", StringComparison.OrdinalIgnoreCase)
                .ShouldBeFalse($"Raw deployment identifiers must not be visible in {file}.");
            source.Contains("tenantId", StringComparison.OrdinalIgnoreCase)
                .ShouldBeFalse($"Raw tenant identifiers must not be visible in {file}.");
        }
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

    private sealed class CurrentContextPersistenceFixture : IDisposable
    {
        private CurrentContextPersistenceFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            Probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            Probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, Probe);
            Migrator = new HostPersistenceMigrator(Factory, Probe);
            Executor = new HostPersistenceCommandExecutor(Factory);
            TransactionScope = new HostPersistenceTransactionScope(Factory);
            Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        }

        public string Root { get; }

        public ProtectedMaterialAvailabilityProbe Probe { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceMigrator Migrator { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public HostPersistenceTransactionScope TransactionScope { get; }

        public static CurrentContextPersistenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-current-context-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new CurrentContextPersistenceFixture(root);
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

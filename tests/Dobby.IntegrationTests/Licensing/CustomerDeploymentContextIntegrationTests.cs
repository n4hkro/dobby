using Dobby.Host;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.CurrentContext;
using Shouldly;

namespace Dobby.IntegrationTests.Licensing;

public sealed class CustomerDeploymentContextIntegrationTests
{
    [Fact]
    public void Host_store_materializes_exactly_one_default_context_for_clean_install()
    {
        using var fixture = CurrentContextPersistenceFixture.Create();
        var store = new HostCustomerDeploymentContextStore(fixture.Executor);
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
        var store = new HostCustomerDeploymentContextStore(fixture.Executor);
        var service = new CurrentCustomerDeploymentContextService(store);

        var result = service.GetCurrentContext();

        result.IsSuccess.ShouldBeFalse();
        result.SupportCode.ShouldBe("missing-current-context");
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM CustomerDeploymentContext;")
            .ShouldBe(0);
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
            Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        }

        public string Root { get; }

        public ProtectedMaterialAvailabilityProbe Probe { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceMigrator Migrator { get; }

        public HostPersistenceCommandExecutor Executor { get; }

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

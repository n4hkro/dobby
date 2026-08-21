using Dobby.Configuration;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Auth;

public sealed class DurableLoginThrottleTests
{
    [Fact]
    public void Account_backoff_survives_service_restart_without_storing_raw_account_identity()
    {
        using var fixture = DurableThrottleFixture.Create();
        var first = fixture.CreateService();
        first.RecordFailure("user-1");
        first.RecordFailure("user-1");
        first.RecordFailure("user-1").RetryAfterSeconds.ShouldBe(10);

        var restarted = fixture.CreateService();

        restarted.Check("user-1").RetryAfterSeconds.ShouldBe(10);
        restarted.Check("user-2").RetryAfterSeconds.ShouldBe(0);
        fixture.Executor.Query<string>(
                "SELECT BucketKey FROM LoginThrottleBuckets ORDER BY BucketKey;")
            .ShouldAllBe(key => !key.Contains("user-1", StringComparison.Ordinal));

        restarted.RecordSuccess("user-1").IsAvailable.ShouldBeTrue();
        restarted.Check("user-1").RetryAfterSeconds.ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM LoginThrottleBuckets WHERE BucketKind = 'device';")
            .ShouldBe(1, "success clears only the account bucket; device abuse history remains bounded by its window");
    }

    [Fact]
    public async Task Concurrent_failures_are_serialized_without_lost_account_or_device_updates()
    {
        using var fixture = DurableThrottleFixture.Create();
        var services = Enumerable.Range(0, 3)
            .Select(_ => fixture.CreateService())
            .ToArray();

        await Task.WhenAll(services.Select(service => Task.Run(() => service.RecordFailure("user-1"))));

        fixture.Executor.QueryScalar<int>(
            "SELECT FailedAttempts FROM LoginThrottleBuckets WHERE BucketKind = 'account';")
            .ShouldBe(3);
        fixture.Executor.QueryScalar<int>(
            "SELECT FailedAttempts FROM LoginThrottleBuckets WHERE BucketKind = 'device';")
            .ShouldBe(3);
        fixture.CreateService().Check("user-1").RetryAfterSeconds.ShouldBe(10);
    }

    private sealed class DurableThrottleFixture : IDisposable
    {
        private DurableThrottleFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
            TransactionScope = new HostPersistenceTransactionScope(factory);
            Time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T00:00:00Z"));
        }

        public string Root { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public HostPersistenceTransactionScope TransactionScope { get; }

        public FakeTimeProvider Time { get; }

        public LoginThrottleService CreateService()
        {
            var auth = new AuthOptions();
            return new LoginThrottleService(
                auth.LoginThrottleStepsSeconds,
                Time,
                new HostLoginThrottleStore(TransactionScope),
                auth.LoginThrottleFailureWindowSeconds,
                auth.LoginGlobalFailureThreshold,
                auth.LoginGlobalBackoffSeconds);
        }

        public static DurableThrottleFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-durable-throttle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new DurableThrottleFixture(root);
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

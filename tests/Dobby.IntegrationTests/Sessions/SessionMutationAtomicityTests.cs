using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Sessions;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Sessions;

public sealed class SessionMutationAtomicityTests
{
    public static TheoryData<string> FailurePoints => new()
    {
        "session-write",
        "audit-write",
        "outbox-write",
        "commit"
    };

    [Theory]
    [MemberData(nameof(FailurePoints))]
    public void Logout_failure_preserves_durable_and_in_memory_session_without_success_evidence(
        string failurePoint)
    {
        using var fixture = SessionMutationFixture.Create();
        var accessor = new ActiveSessionAccessor();
        var service = fixture.CreateService(accessor, new SessionFailureInjector(failurePoint));
        var session = service.Create(fixture.UserId).Value;
        var correlationId = CorrelationId.New();

        var result = service.Logout("user.logout", correlationId);

        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        accessor.Current.ShouldBe(session);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Sessions WHERE SessionId = @SessionId AND InvalidatedAtUtc IS NULL;",
            new { session.SessionId }).ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE CorrelationId = @CorrelationId;",
            new { CorrelationId = correlationId.ToString() }).ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE IdempotencyKey = @IdempotencyKey;",
            new { IdempotencyKey = $"session-mutation:{correlationId}" }).ShouldBe(0);
    }

    [Fact]
    public void Successful_logout_commits_invalidation_audit_and_outbox_before_clearing_memory()
    {
        using var fixture = SessionMutationFixture.Create();
        var accessor = new ActiveSessionAccessor();
        var service = fixture.CreateService(accessor);
        var session = service.Create(fixture.UserId).Value;
        var correlationId = CorrelationId.New();

        service.Logout("user.logout", correlationId).IsSuccess.ShouldBeTrue();

        accessor.Current.ShouldBeNull();
        fixture.Executor.QueryScalar<string>(
            "SELECT InvalidationReason FROM Sessions WHERE SessionId = @SessionId;",
            new { session.SessionId }).ShouldBe("user.logout");
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE CorrelationId = @CorrelationId AND EventName = 'user.logout';",
            new { CorrelationId = correlationId.ToString() }).ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE IdempotencyKey = @IdempotencyKey AND MessageType = 'session.logged-out';",
            new { IdempotencyKey = $"session-mutation:{correlationId}" }).ShouldBe(1);
    }

    private sealed class SessionMutationFixture : IDisposable
    {
        private SessionMutationFixture(string root)
        {
            Root = root;
            Time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T00:00:00Z"));
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(Factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            UserId = Guid.CreateVersion7().ToString("D");
            Executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'session.user', NULL, 0, 1, @Now, @Now);",
                new { UserId, Now = Time.GetUtcNow().ToString("O") });
        }

        public string Root { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public FakeTimeProvider Time { get; }

        public string UserId { get; }

        public SessionLifecycleService CreateService(
            ActiveSessionAccessor accessor,
            IHostPersistenceTransactionFaultInjector? injector = null)
        {
            var audit = new DurableAuditTrail(Executor);
            var unitOfWork = new HostMutationUnitOfWork(
                new HostPersistenceTransactionScope(Factory, injector),
                audit,
                new HostTransactionOutbox(Time));
            var coordinator = new SessionLifecycleAuditCoordinator(audit, Time, unitOfWork);
            return new SessionLifecycleService(
                new SessionStore(Executor),
                accessor,
                new SuccessfulCredentialVerifier(),
                Time,
                new SessionOptions(),
                coordinator);
        }

        public static SessionMutationFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-session-mutation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new SessionMutationFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class SessionFailureInjector(string failurePoint)
        : IHostPersistenceTransactionFaultInjector
    {
        public void BeforeCommand(int commandNumber, HostPersistenceCommandKind kind, string commandText)
        {
            if ((failurePoint == "session-write" && commandText.Contains("UPDATE Sessions", StringComparison.Ordinal)) ||
                (failurePoint == "audit-write" && commandText.Contains("INSERT INTO AuditLog", StringComparison.Ordinal)) ||
                (failurePoint == "outbox-write" && commandText.Contains("INSERT INTO HostOutbox", StringComparison.Ordinal)))
            {
                throw new SyntheticSessionMutationFailure(failurePoint);
            }
        }

        public void BeforeCommit()
        {
            if (failurePoint == "commit")
            {
                throw new SyntheticSessionMutationFailure(failurePoint);
            }
        }
    }

    private sealed class SyntheticSessionMutationFailure(string failurePoint)
        : Exception("Synthetic session mutation failure at " + failurePoint);

    private sealed class SuccessfulCredentialVerifier : IUserCredentialVerifier
    {
        public Result<bool> VerifyPassword(string userId, string password)
        {
            return Result.Success(true);
        }
    }
}

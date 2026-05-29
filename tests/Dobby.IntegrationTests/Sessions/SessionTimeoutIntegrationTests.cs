using Dobby.Configuration;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Dobby.IntegrationTests.Sessions;

public sealed class SessionTimeoutIntegrationTests
{
    [Fact]
    public void Idle_and_absolute_expiry_mark_session_rows_invalidated()
    {
        using var fixture = SessionFixture.Create();
        fixture.InsertUser("user-1");
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var store = new SessionStore(fixture.Executor);
        var service = new SessionLifecycleService(
            store,
            new ActiveSessionAccessor(),
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
        var idle = service.Create("user-1").Value;

        time.Advance(TimeSpan.FromMinutes(30));
        service.Checkpoint().Reason.ShouldBe("idle");

        fixture.Executor.QueryScalar<string>("SELECT InvalidationReason FROM Sessions WHERE SessionId = @SessionId;", new { idle.SessionId }).ShouldBe("idle");

        var absoluteService = new SessionLifecycleService(
            store,
            new ActiveSessionAccessor(),
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30, AutoLockEnabledByDefault = true });
        var absolute = absoluteService.Create("user-1").Value;
        time.Advance(TimeSpan.FromMinutes(480));
        absoluteService.Checkpoint().Reason.ShouldBe("absolute");

        fixture.Executor.QueryScalar<string>("SELECT InvalidationReason FROM Sessions WHERE SessionId = @SessionId;", new { absolute.SessionId }).ShouldBe("absolute");
    }

    [Fact]
    public void Deactivated_user_checkpoint_invalidates_active_session_row()
    {
        using var fixture = SessionFixture.Create();
        fixture.InsertUser("user-1");
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var service = new SessionLifecycleService(
            new SessionStore(fixture.Executor),
            new ActiveSessionAccessor(),
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
        var session = service.Create("user-1").Value;
        fixture.Executor.Execute("UPDATE Users SET IsActive = 0 WHERE UserId = @UserId;", new { session.UserId });

        var checkpoint = service.Checkpoint();

        checkpoint.Kind.ShouldBe(SessionCheckpointKind.Invalidated);
        checkpoint.Reason.ShouldBe("deactivated");
        fixture.Executor.QueryScalar<string>("SELECT InvalidationReason FROM Sessions WHERE SessionId = @SessionId;", new { session.SessionId }).ShouldBe("deactivated");
    }

    [Fact]
    public void Checkpoint_observes_externally_invalidated_active_session_row()
    {
        using var fixture = SessionFixture.Create();
        fixture.InsertUser("user-1");
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var accessor = new ActiveSessionAccessor();
        var service = new SessionLifecycleService(
            new SessionStore(fixture.Executor),
            accessor,
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
        var session = service.Create("user-1").Value;
        fixture.Executor.Execute(
            "UPDATE Sessions SET InvalidatedAtUtc = @InvalidatedAtUtc, InvalidationReason = @InvalidationReason WHERE SessionId = @SessionId;",
            new
            {
                SessionId = session.SessionId,
                InvalidatedAtUtc = time.GetUtcNow().AddMinutes(5).ToString("O"),
                InvalidationReason = "owner-recovery"
            });

        var checkpoint = service.Checkpoint();

        checkpoint.Kind.ShouldBe(SessionCheckpointKind.Invalidated);
        checkpoint.Reason.ShouldBe("owner-recovery");
        accessor.Current.ShouldBeNull();
    }

    private sealed class SessionFixture : IDisposable
    {
        private SessionFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
        }

        public string Root { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public void InsertUser(string userId)
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            Executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
                new { UserId = userId, Now = now });
        }

        public static SessionFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-session-timeout-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new SessionFixture(root);
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

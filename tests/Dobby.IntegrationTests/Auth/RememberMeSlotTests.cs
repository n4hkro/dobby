using Dobby.Configuration;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Dobby.IntegrationTests.Auth;

public sealed class RememberMeSlotTests
{
    [Fact]
    public void Successful_login_with_remember_me_on_replaces_single_username_only_slot()
    {
        using var fixture = RememberMeFixture.Create();
        fixture.InsertUser("user-1", "owner");
        fixture.InsertUser("user-2", "other");
        var service = new RememberMeSlotService(fixture.Executor);

        service.ApplySuccessfulLogin("user-1", "owner", rememberMe: true).IsSuccess.ShouldBeTrue();
        service.ApplySuccessfulLogin("user-2", "other", rememberMe: true).IsSuccess.ShouldBeTrue();

        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot;").ShouldBe(1);
        service.LoadRememberedAccount().Value.ShouldBe(new RememberedLoginAccount("user-2", "other"));
        fixture.Executor.QueryScalar<string>(
            "SELECT UsernameNormalized FROM RememberMeSlot WHERE SlotId = 'single';").ShouldBe("other");
        fixture.Executor.QueryScalar<string>(
            "SELECT Version FROM RememberMeSlot WHERE SlotId = 'single';").ShouldBe("username-only-v1");
    }

    [Fact]
    public void Successful_login_with_remember_me_off_clears_slot_and_explicit_forget_is_idempotent()
    {
        using var fixture = RememberMeFixture.Create();
        fixture.InsertUser("user-1", "owner");
        var service = new RememberMeSlotService(fixture.Executor);
        service.ApplySuccessfulLogin("user-1", "owner", rememberMe: true).IsSuccess.ShouldBeTrue();

        service.ApplySuccessfulLogin("user-1", "owner", rememberMe: false).IsSuccess.ShouldBeTrue();
        service.ClearSlot().IsSuccess.ShouldBeTrue();

        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot;").ShouldBe(0);
        service.LoadRememberedAccount().Value.ShouldBeNull();
    }

    [Fact]
    public void Remembered_username_that_no_longer_matches_user_clears_slot()
    {
        using var fixture = RememberMeFixture.Create();
        fixture.InsertUser("user-1", "owner");
        var service = new RememberMeSlotService(fixture.Executor);
        fixture.Executor.Execute(
            "INSERT INTO RememberMeSlot(SlotId, UserId, UsernameNormalized, Version, CreatedAtUtc, UpdatedAtUtc) VALUES ('single', 'user-1', 'other', 'username-only-v1', @Now, @Now);",
            new { Now = DateTimeOffset.UtcNow.ToString("O") });

        service.LoadRememberedAccount().Value.ShouldBeNull();

        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot;").ShouldBe(0);
    }

    [Fact]
    public void Logout_and_auto_lock_leave_remembered_username_intact()
    {
        using var fixture = RememberMeFixture.Create();
        fixture.InsertUser("user-1", "owner");
        var service = new RememberMeSlotService(fixture.Executor);
        service.ApplySuccessfulLogin("user-1", "owner", rememberMe: true).IsSuccess.ShouldBeTrue();

        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var sessionService = new SessionLifecycleService(
            new SessionStore(fixture.Executor),
            new ActiveSessionAccessor(),
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30, AutoLockEnabledByDefault = true });
        sessionService.Create("user-1").IsSuccess.ShouldBeTrue();

        time.Advance(TimeSpan.FromMinutes(30));
        sessionService.Checkpoint().Kind.ShouldBe(SessionCheckpointKind.Locked);
        sessionService.Logout("user-logout").IsSuccess.ShouldBeTrue();

        service.LoadRememberedAccount().Value.ShouldBe(new RememberedLoginAccount("user-1", "owner"));
    }

    private sealed class RememberMeFixture : IDisposable
    {
        private RememberMeFixture(string root)
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

        public void InsertUser(string userId, string username)
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            Executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, @Username, NULL, 0, 1, @Now, @Now);",
                new { UserId = userId, Username = username, Now = now });
        }

        public static RememberMeFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-remember-me-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new RememberMeFixture(root);
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

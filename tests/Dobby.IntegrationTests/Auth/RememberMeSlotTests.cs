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
    public void Successful_login_with_remember_me_on_replaces_single_protected_slot()
    {
        using var fixture = RememberMeFixture.Create();
        fixture.InsertUser("user-1", "owner");
        fixture.InsertUser("user-2", "other");
        var protector = new RecordingProtector();
        var service = new RememberMeSlotService(fixture.Executor, protector, [1, 2, 3]);
        var serviceWithDifferentApplicationEntropy = new RememberMeSlotService(fixture.Executor, protector, [9, 8, 7]);

        service.ApplySuccessfulLogin("user-1", "owner", "first password", rememberMe: true).IsSuccess.ShouldBeTrue();
        service.ApplySuccessfulLogin("user-2", "other", "second password", rememberMe: true).IsSuccess.ShouldBeTrue();
        serviceWithDifferentApplicationEntropy.ApplySuccessfulLogin("user-2", "other", "third password", rememberMe: true).IsSuccess.ShouldBeTrue();

        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot;").ShouldBe(1);
        var slot = service.LoadSavedLogin().Value;
        slot.ShouldNotBeNull();
        slot.UserId.ShouldBe("user-2");
        slot.MaskedUsername.ShouldBe("other");
        slot.Password.ShouldBe("third password");
        slot.HasMaskedPasswordPrefill.ShouldBeTrue();
        protector.ProtectCalls.ShouldBe(3);
        protector.ProtectEntropies[0].ShouldNotBe(protector.ProtectEntropies[1]);
        protector.ProtectEntropies[1].ShouldNotBe(protector.ProtectEntropies[2]);
    }

    [Fact]
    public void Successful_login_with_remember_me_off_clears_slot_and_explicit_forget_is_idempotent()
    {
        using var fixture = RememberMeFixture.Create();
        fixture.InsertUser("user-1", "owner");
        var service = new RememberMeSlotService(fixture.Executor, new RecordingProtector());
        service.ApplySuccessfulLogin("user-1", "owner", "saved password", rememberMe: true).IsSuccess.ShouldBeTrue();

        service.ApplySuccessfulLogin("user-1", "owner", "ignored password", rememberMe: false).IsSuccess.ShouldBeTrue();
        service.ClearSlot().IsSuccess.ShouldBeTrue();

        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot;").ShouldBe(0);
        service.LoadSavedLogin().Value.ShouldBeNull();
    }

    [Fact]
    public void Successful_login_updates_startup_snapshot_sink_and_remember_me_off_clears_it()
    {
        using var fixture = RememberMeFixture.Create();
        fixture.InsertUser("user-1", "owner");
        var sink = new RecordingStartupSnapshotSink();
        var service = new RememberMeSlotService(fixture.Executor, new RecordingProtector(), startupSnapshotSink: sink);

        service.ApplySuccessfulLogin("user-1", "owner", "saved password", rememberMe: true).IsSuccess.ShouldBeTrue();

        sink.SavedLogin.ShouldBe(new RememberMeSavedLogin("user-1", "owner", "saved password", true));
        sink.ClearCalls.ShouldBe(0);

        service.ApplySuccessfulLogin("user-1", "owner", "ignored password", rememberMe: false).IsSuccess.ShouldBeTrue();

        sink.SavedLogin.ShouldBeNull();
        sink.ClearCalls.ShouldBe(1);
    }

    [Fact]
    public void Loading_saved_login_backfills_or_clears_startup_snapshot_sink()
    {
        using var fixture = RememberMeFixture.Create();
        fixture.InsertUser("user-1", "owner");
        var sink = new RecordingStartupSnapshotSink();
        var service = new RememberMeSlotService(fixture.Executor, new RecordingProtector(), startupSnapshotSink: sink);
        service.ApplySuccessfulLogin("user-1", "owner", "saved password", rememberMe: true).IsSuccess.ShouldBeTrue();
        sink.Reset();

        service.LoadSavedLogin().Value.ShouldBe(new RememberMeSavedLogin("user-1", "owner", "saved password", true));

        sink.SavedLogin.ShouldBe(new RememberMeSavedLogin("user-1", "owner", "saved password", true));
        sink.ClearCalls.ShouldBe(0);

        service.ClearSlot().IsSuccess.ShouldBeTrue();
        sink.Reset();

        service.LoadSavedLogin().Value.ShouldBeNull();

        sink.SavedLogin.ShouldBeNull();
        sink.ClearCalls.ShouldBe(1);
    }

    [Fact]
    public void Unwrap_failure_silently_clears_slot()
    {
        using var fixture = RememberMeFixture.Create();
        fixture.InsertUser("user-1", "owner");
        var service = new RememberMeSlotService(fixture.Executor, new ThrowingUnprotectProtector());
        fixture.Executor.Execute(
            "INSERT INTO RememberMeSlot(SlotId, UserId, MaskedUsername, ProtectedRememberMeBlob, ProtectionVersion, CreatedAtUtc, UpdatedAtUtc, ExpiresAtUtc) VALUES ('single', 'user-1', 'owner', @Blob, 'dpapi-current-user-v1', @Now, @Now, NULL);",
            new { Blob = new byte[] { 1, 2, 3 }, Now = DateTimeOffset.UtcNow.ToString("O") });

        service.LoadSavedLogin().Value.ShouldBeNull();

        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot;").ShouldBe(0);
    }

    [Fact]
    public void Logout_and_auto_lock_leave_saved_login_slot_intact()
    {
        using var fixture = RememberMeFixture.Create();
        fixture.InsertUser("user-1", "owner");
        var service = new RememberMeSlotService(fixture.Executor, new RecordingProtector());
        service.ApplySuccessfulLogin("user-1", "owner", "saved password", rememberMe: true).IsSuccess.ShouldBeTrue();
        var originalBlob = fixture.Executor.Query<byte[]>(
            "SELECT ProtectedRememberMeBlob FROM RememberMeSlot WHERE UserId = @UserId;",
            new { UserId = "user-1" }).Single();

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
        SavedSlotBlob(fixture, "user-1").ShouldBe(originalBlob);

        sessionService.Logout("user-logout").IsSuccess.ShouldBeTrue();

        SavedSlotBlob(fixture, "user-1").ShouldBe(originalBlob);
        service.LoadSavedLogin().Value.ShouldBe(new RememberMeSavedLogin("user-1", "owner", "saved password", true));
    }

    private static byte[] SavedSlotBlob(RememberMeFixture fixture, string userId)
    {
        return fixture.Executor.Query<byte[]>(
            "SELECT ProtectedRememberMeBlob FROM RememberMeSlot WHERE UserId = @UserId;",
            new { UserId = userId }).Single();
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

    private sealed class RecordingProtector : IProtectedDataProtector
    {
        private static readonly byte[] ProtectedPrefix = "protected:"u8.ToArray();

        public int ProtectCalls { get; private set; }

        public List<byte[]> ProtectEntropies { get; } = [];

        public byte[] Protect(byte[] plaintext, byte[] entropy)
        {
            ProtectCalls++;
            ProtectEntropies.Add(entropy.ToArray());
            return ProtectedPrefix.Concat(plaintext.Select(value => (byte)(value ^ 0x24))).ToArray();
        }

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy)
        {
            return protectedBlob.Skip(ProtectedPrefix.Length).Select(value => (byte)(value ^ 0x24)).ToArray();
        }
    }

    private sealed class ThrowingUnprotectProtector : IProtectedDataProtector
    {
        public byte[] Protect(byte[] plaintext, byte[] entropy) => plaintext;

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy) => throw new InvalidOperationException("unwrap failure");
    }

    private sealed class RecordingStartupSnapshotSink : IRememberMeStartupSnapshotSink
    {
        public RememberMeSavedLogin? SavedLogin { get; private set; }

        public int ClearCalls { get; private set; }

        public void Save(RememberMeSavedLogin savedLogin)
        {
            SavedLogin = savedLogin;
        }

        public void Clear()
        {
            SavedLogin = null;
            ClearCalls++;
        }

        public void Reset()
        {
            SavedLogin = null;
            ClearCalls = 0;
        }
    }
}

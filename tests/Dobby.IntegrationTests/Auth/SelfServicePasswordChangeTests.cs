using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.IntegrationTests.Auth;

public sealed class SelfServicePasswordChangeTests
{
    [Fact]
    public void Active_user_changes_password_without_current_password_and_preserves_active_session()
    {
        using var fixture = PasswordChangeFixture.Create();
        var userId = fixture.InsertUserWithCredential("old password");
        fixture.InsertActiveSession(userId, "session-1", version: 1);
        var coordinator = fixture.CreateCoordinator();

        var result = coordinator.ChangePassword(userId, "session-1", "new password", rememberMe: true, CorrelationId.New());

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.ErrorCode.ToString());
        fixture.CredentialStore.VerifyPassword(userId, "old password").Value.ShouldBeFalse();
        fixture.CredentialStore.VerifyPassword(userId, "new password").Value.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT SessionVersion FROM Sessions WHERE SessionId = 'session-1';").ShouldBe(2);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE SessionId = 'session-1' AND InvalidatedAtUtc IS NULL;").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot WHERE UserId = @UserId;", new { UserId = userId }).ShouldBe(1);
        fixture.RememberMe.LoadSavedLogin().Value!.Password.ShouldBe("new password");
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.password.change' AND Result = 'success';").ShouldBe(1);
    }

    [Fact]
    public void Password_change_requires_matching_active_session_before_rebinding_credentials()
    {
        using var fixture = PasswordChangeFixture.Create();
        var userId = fixture.InsertUserWithCredential("old password");
        fixture.InsertActiveSession(userId, "session-1", version: 1);
        var coordinator = fixture.CreateCoordinator();

        var result = coordinator.ChangePassword(userId, "missing-session", "new password", rememberMe: true, CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.NotFound);
        fixture.CredentialStore.VerifyPassword(userId, "old password").Value.ShouldBeTrue();
        fixture.CredentialStore.VerifyPassword(userId, "new password").Value.ShouldBeFalse();
        fixture.Executor.QueryScalar<int>("SELECT SessionVersion FROM Sessions WHERE SessionId = 'session-1';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.password.change';").ShouldBe(0);
    }

    private sealed class PasswordChangeFixture : IDisposable
    {
        private readonly RecordingProtector protector = new();
        private readonly KdfPolicyResolver resolver = new(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" });

        private PasswordChangeFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
            CredentialStore = new CredentialStore(Executor, protector, resolver);
            RememberMe = new RememberMeSlotService(Executor, protector);
        }

        public string Root { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public CredentialStore CredentialStore { get; }

        public RememberMeSlotService RememberMe { get; }

        public string InsertUserWithCredential(string password)
        {
            var userId = Guid.CreateVersion7().ToString("D");
            var now = DateTimeOffset.UtcNow;
            Executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
                new { UserId = userId, Now = now.ToString("O") });
            CredentialStore.CreateCredential(userId, password, now).IsSuccess.ShouldBeTrue();
            return userId;
        }

        public void InsertActiveSession(string userId, string sessionId, int version)
        {
            var now = DateTimeOffset.UtcNow;
            Executor.Execute(
                "INSERT INTO Sessions(SessionId, UserId, CreatedAtUtc, LastActivityAtUtc, ExpiresAtUtc, InvalidatedAtUtc, InvalidationReason, SessionVersion) VALUES (@SessionId, @UserId, @Now, @Now, @ExpiresAtUtc, NULL, NULL, @SessionVersion);",
                new { SessionId = sessionId, UserId = userId, Now = now.ToString("O"), ExpiresAtUtc = now.AddHours(8).ToString("O"), SessionVersion = version });
        }

        public SelfServicePasswordChangeCoordinator CreateCoordinator()
        {
            return new SelfServicePasswordChangeCoordinator(Executor, CredentialStore, RememberMe, new DurableAuditTrail(Executor));
        }

        public static PasswordChangeFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-password-change-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new PasswordChangeFixture(root);
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

        public byte[] Protect(byte[] plaintext, byte[] entropy)
        {
            return ProtectedPrefix.Concat(plaintext.Select(value => (byte)(value ^ 0x5A))).ToArray();
        }

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy)
        {
            return protectedBlob.Skip(ProtectedPrefix.Length).Select(value => (byte)(value ^ 0x5A)).ToArray();
        }
    }
}

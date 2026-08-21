using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Auth;

public sealed class SelfServicePasswordChangeTests
{
    public static TheoryData<string> AtomicFailurePoints => new()
    {
        "credential",
        "other-sessions",
        "current-session",
        "remember-account",
        "audit",
        "outbox",
        "commit"
    };

    [Fact]
    public void Verified_user_changes_password_preserves_current_session_and_revokes_other_sessions()
    {
        using var fixture = PasswordChangeFixture.Create();
        var userId = fixture.InsertUserWithCredential("old password");
        fixture.InsertActiveSession(userId, "session-1", version: 1);
        fixture.InsertActiveSession(userId, "session-2", version: 7);

        var result = fixture.CreateCoordinator().ChangePassword(
            userId,
            "session-1",
            "old password",
            "new password",
            "new password",
            rememberMe: true,
            CorrelationId.New());

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.ErrorCode.ToString());
        fixture.CredentialStore.VerifyPassword(userId, "old password").Value.ShouldBeFalse();
        fixture.CredentialStore.VerifyPassword(userId, "new password").Value.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>(
            "SELECT SessionVersion FROM Sessions WHERE SessionId = 'session-1';").ShouldBe(2);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Sessions WHERE SessionId = 'session-1' AND InvalidatedAtUtc IS NULL;").ShouldBe(1);
        fixture.Executor.QueryScalar<string>(
            "SELECT InvalidationReason FROM Sessions WHERE SessionId = 'session-2';").ShouldBe("password-change");
        fixture.RememberMe.LoadRememberedAccount().Value.ShouldBe(new RememberedLoginAccount(userId, "owner"));
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.password.change' AND Result = 'success';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE Purpose = 'security.session' AND MessageType = 'credential.changed';").ShouldBe(1);
    }

    [Fact]
    public void Wrong_current_password_returns_non_disclosing_failure_and_records_durable_throttle()
    {
        using var fixture = PasswordChangeFixture.Create();
        var userId = fixture.InsertUserWithCredential("old password");
        fixture.InsertActiveSession(userId, "session-1", version: 1);

        var result = fixture.CreateCoordinator().ChangePassword(
            userId,
            "session-1",
            "wrong password",
            "new password",
            "new password",
            rememberMe: false,
            CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.Unauthorized);
        fixture.CredentialStore.VerifyPassword(userId, "old password").Value.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'security.verification.failed';")
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT SUM(FailedAttempts) FROM LoginThrottleBuckets WHERE BucketKind = 'account';")
            .ShouldBe(1);
    }

    [Fact]
    public void Confirmation_mismatch_fails_before_credential_verification_or_throttle_write()
    {
        using var fixture = PasswordChangeFixture.Create();
        var userId = fixture.InsertUserWithCredential("old password");
        fixture.InsertActiveSession(userId, "session-1", version: 1);

        var result = fixture.CreateCoordinator().ChangePassword(
            userId,
            "session-1",
            "old password",
            "new password",
            "different password",
            rememberMe: false,
            CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM LoginThrottleBuckets;").ShouldBe(0);
        fixture.CredentialStore.VerifyPassword(userId, "old password").Value.ShouldBeTrue();
    }

    [Fact]
    public void Password_change_requires_matching_active_session_before_reauthentication()
    {
        using var fixture = PasswordChangeFixture.Create();
        var userId = fixture.InsertUserWithCredential("old password");
        fixture.InsertActiveSession(userId, "session-1", version: 1);

        var result = fixture.CreateCoordinator().ChangePassword(
            userId,
            "missing-session",
            "old password",
            "new password",
            "new password",
            rememberMe: true,
            CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.NotFound);
        fixture.CredentialStore.VerifyPassword(userId, "old password").Value.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM LoginThrottleBuckets;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.password.change';")
            .ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(AtomicFailurePoints))]
    public void Failure_at_each_write_or_commit_preserves_prior_effective_state(string failurePoint)
    {
        using var fixture = PasswordChangeFixture.Create();
        var userId = fixture.InsertUserWithCredential("old password");
        fixture.InsertActiveSession(userId, "session-1", version: 1);
        fixture.InsertActiveSession(userId, "session-2", version: 4);

        var result = fixture.CreateCoordinator(new PasswordChangeFailureInjector(failurePoint)).ChangePassword(
            userId,
            "session-1",
            "old password",
            "new password",
            "new password",
            rememberMe: true,
            CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        fixture.CredentialStore.VerifyPassword(userId, "old password").Value.ShouldBeTrue();
        fixture.CredentialStore.VerifyPassword(userId, "new password").Value.ShouldBeFalse();
        fixture.Executor.QueryScalar<int>(
            "SELECT SessionVersion FROM Sessions WHERE SessionId = 'session-1';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Sessions WHERE SessionId = 'session-2' AND InvalidatedAtUtc IS NULL;").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.password.change';").ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE Purpose = 'security.session';").ShouldBe(0);
    }

    [Fact]
    public void Account_settings_shortcut_requires_current_password_and_confirmation()
    {
        using var fixture = PasswordChangeFixture.Create();
        var userId = fixture.InsertUserWithCredential("old password");
        fixture.InsertActiveSession(userId, "session-1", version: 1);
        var coordinator = fixture.CreateCoordinator();

        var result = fixture.CreateAccountSettingsModel(coordinator).ChangePassword(
            userId,
            "session-1",
            "old password",
            "new password",
            "new password",
            rememberMe: true,
            CorrelationId.New());

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.ErrorCode.ToString());
        fixture.CredentialStore.VerifyPassword(userId, "new password").Value.ShouldBeTrue();
    }

    private sealed class PasswordChangeFixture : IDisposable
    {
        private readonly RecordingProtector protector = new();
        private readonly KdfPolicyResolver resolver = new(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" });

        private PasswordChangeFixture(string root)
        {
            Root = root;
            Time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T00:00:00Z"));
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(Factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            CredentialStore = new CredentialStore(Executor, protector, resolver, timeProvider: Time);
            RememberMe = new RememberMeSlotService(Executor);
        }

        public string Root { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public CredentialStore CredentialStore { get; }

        public RememberMeSlotService RememberMe { get; }

        public FakeTimeProvider Time { get; }

        public string InsertUserWithCredential(string password)
        {
            var userId = Guid.CreateVersion7().ToString("D");
            var now = Time.GetUtcNow();
            Executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
                new { UserId = userId, Now = now.ToString("O") });
            CredentialStore.CreateCredential(userId, password, now).IsSuccess.ShouldBeTrue();
            return userId;
        }

        public void InsertActiveSession(string userId, string sessionId, int version)
        {
            var now = Time.GetUtcNow();
            Executor.Execute(
                "INSERT INTO Sessions(SessionId, UserId, CreatedAtUtc, LastActivityAtUtc, ExpiresAtUtc, InvalidatedAtUtc, InvalidationReason, SessionVersion) VALUES (@SessionId, @UserId, @Now, @Now, @ExpiresAtUtc, NULL, NULL, @SessionVersion);",
                new
                {
                    SessionId = sessionId,
                    UserId = userId,
                    Now = now.ToString("O"),
                    ExpiresAtUtc = now.AddHours(8).ToString("O"),
                    SessionVersion = version
                });
        }

        public SelfServicePasswordChangeCoordinator CreateCoordinator(
            IHostPersistenceTransactionFaultInjector? injector = null)
        {
            var audit = new DurableAuditTrail(Executor);
            var transactionScope = new HostPersistenceTransactionScope(Factory, injector);
            var mutationUnitOfWork = new HostMutationUnitOfWork(
                transactionScope,
                audit,
                new HostTransactionOutbox(Time));
            var throttle = new LoginThrottleService(
                [0, 0, 10, 30, 60],
                Time,
                new HostLoginThrottleStore(new HostPersistenceTransactionScope(Factory)));
            return new SelfServicePasswordChangeCoordinator(
                Executor,
                CredentialStore,
                RememberMe,
                throttle,
                mutationUnitOfWork,
                audit,
                Time);
        }

        public Dobby.Host.Settings.AccountSettingsModel CreateAccountSettingsModel(
            SelfServicePasswordChangeCoordinator coordinator)
        {
            return new Dobby.Host.Settings.AccountSettingsModel(coordinator.ChangePassword);
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

    private sealed class PasswordChangeFailureInjector(string failurePoint)
        : IHostPersistenceTransactionFaultInjector
    {
        public void BeforeCommand(int commandNumber, HostPersistenceCommandKind kind, string commandText)
        {
            if ((failurePoint == "credential" && commandText.Contains("UPDATE Credentials", StringComparison.Ordinal)) ||
                (failurePoint == "other-sessions" && commandText.Contains("SessionId <>", StringComparison.Ordinal)) ||
                (failurePoint == "current-session" && commandText.Contains("SessionVersion = SessionVersion + 1", StringComparison.Ordinal)) ||
                (failurePoint == "remember-account" && commandText.Contains("INSERT INTO RememberMeSlot", StringComparison.Ordinal)) ||
                (failurePoint == "audit" && commandText.Contains("INSERT INTO AuditLog", StringComparison.Ordinal)) ||
                (failurePoint == "outbox" && commandText.Contains("INSERT INTO HostOutbox", StringComparison.Ordinal)))
            {
                throw new SyntheticPasswordChangeFailure(failurePoint);
            }
        }

        public void BeforeCommit()
        {
            if (failurePoint == "commit")
            {
                throw new SyntheticPasswordChangeFailure(failurePoint);
            }
        }
    }

    private sealed class SyntheticPasswordChangeFailure(string failurePoint)
        : Exception("Synthetic password-change failure at " + failurePoint);

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

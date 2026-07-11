using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Startup;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Auth;

public sealed class LoginServiceTests
{
    [Fact]
    public void First_clean_install_login_bootstraps_owner_then_returns_success()
    {
        using var fixture = LoginServiceFixture.Create();
        var service = fixture.CreateLoginService();

        var result = service.Login(" Owner ", "first owner password", "Əli Məmmədov", CorrelationId.New());

        result.Kind.ShouldBe(LoginAttemptKind.Success);
        result.UserId.ShouldNotBeNullOrWhiteSpace();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Users WHERE IsOwner = 1 AND UsernameNormalized = 'owner';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.create';").ShouldBe(1);
    }

    [Fact]
    public void Existing_user_login_succeeds_with_password_and_resets_throttle()
    {
        using var fixture = LoginServiceFixture.Create();
        var service = fixture.CreateLoginService();
        service.Login("owner", "first owner password", null, CorrelationId.New()).Kind.ShouldBe(LoginAttemptKind.Success);
        fixture.Throttle.RecordFailure("owner");
        fixture.Throttle.RecordFailure("owner");
        fixture.Throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(10);
        fixture.Time.Advance(TimeSpan.FromSeconds(10));

        var result = service.Login("OWNER", "first owner password", null, CorrelationId.New());

        result.Kind.ShouldBe(LoginAttemptKind.Success);
        fixture.Throttle.Check().RetryAfterSeconds.ShouldBe(0);
    }

    [Fact]
    public void Invalid_password_returns_non_disclosing_failure_and_increments_throttle()
    {
        using var fixture = LoginServiceFixture.Create();
        var service = fixture.CreateLoginService();
        service.Login("owner", "first owner password", null, CorrelationId.New()).Kind.ShouldBe(LoginAttemptKind.Success);

        var first = service.Login("owner", "wrong", null, CorrelationId.New());
        var second = service.Login("other", "wrong", null, CorrelationId.New());
        var third = service.Login("owner", "wrong", null, CorrelationId.New());

        first.Kind.ShouldBe(LoginAttemptKind.InvalidCredentials);
        second.Kind.ShouldBe(LoginAttemptKind.InvalidCredentials);
        third.Kind.ShouldBe(LoginAttemptKind.InvalidCredentials);
        third.RetryAfterSeconds.ShouldBe(10);
    }

    [Fact]
    public void Throttled_state_blocks_login_until_countdown_expires()
    {
        using var fixture = LoginServiceFixture.Create();
        var service = fixture.CreateLoginService();
        service.Login("owner", "first owner password", null, CorrelationId.New()).Kind.ShouldBe(LoginAttemptKind.Success);
        service.Login("owner", "wrong", null, CorrelationId.New());
        service.Login("owner", "wrong", null, CorrelationId.New());
        service.Login("owner", "wrong", null, CorrelationId.New()).RetryAfterSeconds.ShouldBe(10);

        var throttled = service.Login("owner", "first owner password", null, CorrelationId.New());
        fixture.Time.Advance(TimeSpan.FromSeconds(10));
        var accepted = service.Login("owner", "first owner password", null, CorrelationId.New());

        throttled.Kind.ShouldBe(LoginAttemptKind.Throttled);
        throttled.RetryAfterSeconds.ShouldBe(10);
        accepted.Kind.ShouldBe(LoginAttemptKind.Success);
    }

    [Fact]
    public void Deactivated_user_fails_without_disclosing_credential_state()
    {
        using var fixture = LoginServiceFixture.Create();
        var service = fixture.CreateLoginService();
        service.Login("owner", "first owner password", null, CorrelationId.New()).Kind.ShouldBe(LoginAttemptKind.Success);
        fixture.Executor.Execute("UPDATE Users SET IsActive = 0 WHERE UsernameNormalized = 'owner';");

        var result = service.Login("owner", "first owner password", null, CorrelationId.New());

        result.Kind.ShouldBe(LoginAttemptKind.Deactivated);
    }

    [Fact]
    public void Unsupported_kdf_policy_fails_closed()
    {
        using var fixture = LoginServiceFixture.Create(passwordKdfMode: "Argon2id");
        var service = fixture.CreateLoginService();

        var result = service.Login("owner", "first owner password", null, CorrelationId.New());

        result.Kind.ShouldBe(LoginAttemptKind.KdfUnsupported);
    }

    [Fact]
    public void Unauthorized_verifier_failure_returns_validation_failed()
    {
        using var fixture = LoginServiceFixture.Create();
        fixture.CreateLoginService()
            .Login("owner", "first owner password", null, CorrelationId.New())
            .Kind.ShouldBe(LoginAttemptKind.Success);
        var service = fixture.CreateLoginService(new UnauthorizedCredentialVerifier());

        var result = service.Login("owner", "first owner password", null, CorrelationId.New());

        result.Kind.ShouldBe(LoginAttemptKind.ValidationFailed);
    }

    private sealed class LoginServiceFixture : IDisposable
    {
        private readonly RecordingProtector protector = new();
        private readonly string passwordKdfMode;

        private LoginServiceFixture(string root, string passwordKdfMode)
        {
            Root = root;
            this.passwordKdfMode = passwordKdfMode;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(Factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            Time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
            Throttle = new LoginThrottleService([0, 0, 10, 30, 60], Time);
        }

        public string Root { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public FakeTimeProvider Time { get; }

        public LoginThrottleService Throttle { get; }

        public LoginService CreateLoginService(IUserCredentialVerifier? credentialVerifier = null)
        {
            var authOptions = new AuthOptions { PasswordKdfMode = passwordKdfMode };
            var resolver = new KdfPolicyResolver(authOptions);
            var credentialStore = new CredentialStore(Executor, protector, resolver);
            var bootstrap = new OwnerBootstrapCoordinator(
                new HostPersistenceTransactionScope(Factory),
                new OwnerBootstrapService(protector, resolver),
                new DurableAuditTrail(Executor),
                Time);
            return new LoginService(Executor, resolver, credentialVerifier ?? credentialStore, bootstrap, Throttle);
        }

        public static LoginServiceFixture Create(string passwordKdfMode = "Pbkdf2Sha256")
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-login-service-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new LoginServiceFixture(root, passwordKdfMode);
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

    private sealed class UnauthorizedCredentialVerifier : IUserCredentialVerifier
    {
        public Result<bool> VerifyPassword(string userId, string password)
        {
            return Result.Failure<bool>(ErrorCode.Unauthorized);
        }
    }
}

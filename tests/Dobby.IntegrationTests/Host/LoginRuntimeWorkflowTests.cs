using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Host.Startup;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Host;

public sealed class LoginRuntimeWorkflowTests
{
    [Fact]
    public void Submit_successful_first_owner_login_creates_session_remember_slot_and_login_audit()
    {
        using var fixture = LoginRuntimeFixture.Create();
        var workflow = fixture.CreateWorkflow();

        var result = workflow.Submit(new LoginSubmission(
            " Owner ",
            "first owner password",
            "Əli Məmmədov",
            RememberMe: true,
            CorrelationId.New()));

        result.IsAuthenticated.ShouldBeTrue();
        result.Session.ShouldNotBeNull();
        result.Session.UserId.ShouldNotBeNullOrWhiteSpace();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE UserId = @UserId AND InvalidatedAtUtc IS NULL;", new { result.Session.UserId }).ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot WHERE UserId = @UserId;", new { result.Session.UserId }).ShouldBe(1);
        fixture.Executor.QueryScalar<string>("SELECT DisplayName FROM Users WHERE UserId = @UserId;", new { result.Session.UserId }).ShouldBe("Əli Məmmədov");
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.login' AND ActorId = @UserId AND Result = 'success';", new { result.Session.UserId }).ShouldBe(1);
    }

    [Fact]
    public void Submit_failed_existing_login_stays_unauthenticated_and_does_not_create_session()
    {
        using var fixture = LoginRuntimeFixture.Create();
        fixture.BootstrapOwner("owner", "correct password");
        var workflow = fixture.CreateWorkflow();

        var result = workflow.Submit(new LoginSubmission(
            "owner",
            "wrong password",
            DisplayName: null,
            RememberMe: false,
            CorrelationId.New()));

        result.IsAuthenticated.ShouldBeFalse();
        result.AttemptKind.ShouldBe(LoginAttemptKind.InvalidCredentials);
        result.Session.ShouldBeNull();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.login.failed' AND Result = 'failed';").ShouldBe(1);
    }

    [Fact]
    public void Submit_does_not_mutate_remember_me_when_session_creation_fails()
    {
        using var fixture = LoginRuntimeFixture.Create();
        fixture.BootstrapOwner("owner", "correct password");
        fixture.SetExistingActiveSession("other-user");
        var workflow = fixture.CreateWorkflow();

        var result = workflow.Submit(new LoginSubmission(
            "owner",
            "correct password",
            DisplayName: null,
            RememberMe: true,
            CorrelationId.New()));

        result.IsAuthenticated.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.AlreadyExists);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot;").ShouldBe(0);
    }

    private sealed class LoginRuntimeFixture : IDisposable
    {
        private readonly RecordingProtector credentialProtector = new();
        private readonly RecordingProtector rememberMeProtector = new();
        private readonly KdfPolicyResolver kdfPolicyResolver = new(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" });
        private readonly ActiveSessionAccessor activeSessionAccessor = new();

        private LoginRuntimeFixture(string root)
        {
            Root = root;
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

        public LoginRuntimeWorkflow CreateWorkflow()
        {
            var credentialStore = new CredentialStore(Executor, credentialProtector, kdfPolicyResolver);
            var loginService = new LoginService(Executor, credentialStore, CreateOwnerBootstrapCoordinator(), Throttle);
            var sessionService = new SessionLifecycleService(
                new SessionStore(Executor),
                activeSessionAccessor,
                credentialStore,
                Time,
                new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
            var rememberMeSlot = new RememberMeSlotService(Executor, rememberMeProtector);
            var audit = new LoginAuditCoordinator(new DurableAuditTrail(Executor), Time);
            return new LoginRuntimeWorkflow(loginService, sessionService, rememberMeSlot, audit);
        }

        public void BootstrapOwner(string username, string password)
        {
            CreateOwnerBootstrapCoordinator()
                .BootstrapFirstOwner(username, null, password, CorrelationId.New())
                .IsSuccess.ShouldBeTrue();
        }

        public void SetExistingActiveSession(string userId)
        {
            activeSessionAccessor.SetActive(new SessionRecord(
                "already-active",
                userId,
                Time.GetUtcNow(),
                Time.GetUtcNow(),
                Time.GetUtcNow().AddHours(8),
                null,
                1)).IsSuccess.ShouldBeTrue();
        }

        public static LoginRuntimeFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-login-runtime-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new LoginRuntimeFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private OwnerBootstrapCoordinator CreateOwnerBootstrapCoordinator()
        {
            return new OwnerBootstrapCoordinator(
                new HostPersistenceTransactionScope(Factory),
                new OwnerBootstrapService(credentialProtector, kdfPolicyResolver),
                new DurableAuditTrail(Executor),
                Time);
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
            protectedBlob.Take(ProtectedPrefix.Length).SequenceEqual(ProtectedPrefix).ShouldBeTrue();
            return protectedBlob.Skip(ProtectedPrefix.Length).Select(value => (byte)(value ^ 0x5A)).ToArray();
        }
    }
}

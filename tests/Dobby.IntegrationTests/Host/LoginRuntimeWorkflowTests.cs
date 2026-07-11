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
        fixture.LoadSavedLogin().ShouldBe(new RememberMeSavedLogin(
            result.Session.UserId,
            "owner",
            "first owner password",
            HasMaskedPasswordPrefill: true));
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
    public void Submit_existing_login_runs_inside_transaction_scope()
    {
        using var fixture = LoginRuntimeFixture.Create();
        fixture.BootstrapOwner("owner", "correct password");
        var transactionScope = new RecordingTransactionScope(fixture.Executor);
        var workflow = fixture.CreateWorkflow(transactionScope);

        var result = workflow.Submit(new LoginSubmission(
            "owner",
            "correct password",
            DisplayName: null,
            RememberMe: true,
            CorrelationId.New()));

        result.IsAuthenticated.ShouldBeTrue();
        transactionScope.ExecutionCount.ShouldBe(1);
    }

    [Fact]
    public void Submit_first_owner_login_runs_inside_transaction_scope_without_nested_transaction_failure()
    {
        using var fixture = LoginRuntimeFixture.Create();
        var transactionScope = new HostPersistenceTransactionScope(fixture.Factory);
        var workflow = fixture.CreateWorkflow(transactionScope);

        var result = workflow.Submit(new LoginSubmission(
            " Owner ",
            "first owner password",
            "Əli Məmmədov",
            RememberMe: true,
            CorrelationId.New()));

        result.IsAuthenticated.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Users WHERE IsOwner = 1 AND UsernameNormalized = 'owner';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE UserId = @UserId AND InvalidatedAtUtc IS NULL;", new { result.Session!.UserId }).ShouldBe(1);
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

    [Fact]
    public void Submit_reuses_completed_credential_warmup_without_repeating_password_verification()
    {
        using var fixture = LoginRuntimeFixture.Create();
        fixture.InsertUser("user-1", "owner", isActive: true);
        var verifier = new CountingCredentialVerifier(succeeds: true);
        var workflow = fixture.CreateWorkflow(credentialVerifier: verifier);

        workflow.WarmCredentials(new LoginCredentialWarmup(
            "owner",
            "correct password",
            CorrelationId.New()));

        System.Threading.SpinWait.SpinUntil(
                () => verifier.VerifyCount == 1,
                TimeSpan.FromSeconds(5))
            .ShouldBeTrue();
        WaitForCompletedCredentialWarmup(workflow);

        var result = workflow.Submit(new LoginSubmission(
            "owner",
            "correct password",
            DisplayName: null,
            RememberMe: true,
            CorrelationId.New()));

        result.IsAuthenticated.ShouldBeTrue();
        verifier.VerifyCount.ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE UserId = 'user-1' AND InvalidatedAtUtc IS NULL;").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.login' AND ActorId = 'user-1' AND Result = 'success';").ShouldBe(1);
    }

    private static void WaitForCompletedCredentialWarmup(LoginRuntimeWorkflow workflow)
    {
        var warmedCredential = typeof(LoginRuntimeWorkflow).GetField(
            "warmedCredential",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        warmedCredential.ShouldNotBeNull();

        System.Threading.SpinWait.SpinUntil(
                () => warmedCredential.GetValue(workflow) is not null,
                TimeSpan.FromSeconds(5))
            .ShouldBeTrue();
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

        public LoginRuntimeWorkflow CreateWorkflow(
            IHostPersistenceTransactionScope? transactionScope = null,
            IUserCredentialVerifier? credentialVerifier = null)
        {
            var credentialStore = new CredentialStore(Executor, credentialProtector, kdfPolicyResolver);
            var verifier = credentialVerifier ?? credentialStore;
            var loginService = new LoginService(Executor, kdfPolicyResolver, verifier, CreateOwnerBootstrapCoordinator(), Throttle);
            var sessionService = new SessionLifecycleService(
                new SessionStore(Executor),
                activeSessionAccessor,
                verifier,
                Time,
                new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
            var rememberMeSlot = new RememberMeSlotService(Executor, rememberMeProtector);
            var audit = new LoginAuditCoordinator(new DurableAuditTrail(Executor), Time);
            return new LoginRuntimeWorkflow(loginService, sessionService, rememberMeSlot, audit, transactionScope);
        }

        public RememberMeSavedLogin? LoadSavedLogin()
        {
            return new RememberMeSlotService(Executor, rememberMeProtector).LoadSavedLogin().Value;
        }

        public void BootstrapOwner(string username, string password)
        {
            CreateOwnerBootstrapCoordinator()
                .BootstrapFirstOwner(username, null, password, CorrelationId.New())
                .IsSuccess.ShouldBeTrue();
        }

        public void InsertUser(string userId, string usernameNormalized, bool isActive)
        {
            var now = Time.GetUtcNow().ToString("O");
            Executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, @UsernameNormalized, NULL, 1, @IsActive, @Now, @Now);",
                new
                {
                    UserId = userId,
                    UsernameNormalized = usernameNormalized,
                    IsActive = isActive ? 1 : 0,
                    Now = now
                });
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

    private sealed class CountingCredentialVerifier(bool succeeds) : IUserCredentialVerifier
    {
        private int verifyCount;

        public int VerifyCount => Volatile.Read(ref verifyCount);

        public Result<bool> VerifyPassword(string userId, string password)
        {
            Interlocked.Increment(ref verifyCount);
            return Result.Success(succeeds);
        }
    }

    private sealed class RecordingTransactionScope(IHostPersistenceCommandExecutor executor) : IHostPersistenceTransactionScope
    {
        public int ExecutionCount { get; private set; }

        public void ExecuteInTransaction(Action<IHostPersistenceCommandExecutor> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            ExecutionCount++;
            action(executor);
        }
    }
}

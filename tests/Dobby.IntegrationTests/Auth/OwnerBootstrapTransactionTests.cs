using System.Text.Json;
using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Startup;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Shouldly;
using System.Threading;
using System.Threading.Tasks;

namespace Dobby.IntegrationTests.Auth;

[Collection(OwnerBootstrapConcurrencyCollection.Name)]
public sealed class OwnerBootstrapTransactionTests
{
    [Fact]
    public void Bootstrap_first_owner_creates_owner_credentials_audit_and_no_permission_row_atomically()
    {
        using var fixture = OwnerBootstrapFixture.Create();
        var coordinator = fixture.CreateCoordinator(new DurableAuditTrail(fixture.Executor));

        var result = coordinator.BootstrapFirstOwner(" Owner ", " Əli Məmmədov ", "first owner password", CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        var user = fixture.Executor.Query<UserRow>("SELECT UserId, UsernameNormalized, DisplayName, IsOwner, IsActive FROM Users;").Single();
        user.UsernameNormalized.ShouldBe("owner");
        user.DisplayName.ShouldBe("Əli Məmmədov");
        user.IsOwner.ShouldBe(1);
        user.IsActive.ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Permissions WHERE UserId = @UserId;", new { user.UserId }).ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Credentials WHERE UserId = @UserId;", new { user.UserId }).ShouldBe(1);
        fixture.CredentialStore.VerifyPassword(user.UserId, "first owner password").Value.ShouldBeTrue();

        var audit = fixture.Executor.Query<AuditRow>("SELECT EventName, ActorId, Target, Result, DetailsJson FROM AuditLog;").Single();
        audit.EventName.ShouldBe(AuditEventName.UserCreate);
        audit.ActorId.ShouldBe("system");
        audit.Target.ShouldBe(user.UserId);
        audit.Result.ShouldBe("success");
        var details = JsonSerializer.Deserialize<Dictionary<string, string>>(audit.DetailsJson)!;
        details.OrderBy(item => item.Key).Select(item => item.Key).ShouldBe([
            "bootstrapKind",
            "composition",
            "permissionState",
            "principalRole"
        ]);
        details["bootstrapKind"].ShouldBe("first-user-owner");
        details["principalRole"].ShouldBe("Owner");
        details["permissionState"].ShouldBe("is-owner-short-circuit");
        details["composition"].ShouldBe("B04-transaction-aware-host-audit");
    }

    [Fact]
    public void Bootstrap_rolls_back_users_credentials_and_audit_when_audit_append_fails()
    {
        using var fixture = OwnerBootstrapFixture.Create();
        var coordinator = fixture.CreateCoordinator(new ThrowingTransactionAuditTrail());

        var result = coordinator.BootstrapFirstOwner("owner", null, "first owner password", CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Users;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Credentials;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Permissions;").ShouldBe(0);
    }

    [Fact]
    public async Task Concurrent_bootstrap_attempts_are_serialized_and_create_only_one_owner()
    {
        var transactionScope = new PausingTransactionScope();
        var coordinator = new OwnerBootstrapCoordinator(
            transactionScope,
            new OwnerBootstrapService(new RecordingProtector(), new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" })),
            new DurableAuditTrail(transactionScope.Executor),
            TimeProvider.System);

        var first = Task.Factory.StartNew(
            () => coordinator.BootstrapFirstOwner("owner-one", null, "first owner password", CorrelationId.New()),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await transactionScope.FirstTransactionEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var second = Task.Factory.StartNew(
            () => coordinator.BootstrapFirstOwner("owner-two", null, "second owner password", CorrelationId.New()),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await Task.Delay(100);
        transactionScope.ReleaseFirstTransaction();

        var results = await Task.WhenAll(first, second);

        transactionScope.MaxConcurrentTransactions.ShouldBe(1);
        results.Count(result => result.IsSuccess).ShouldBe(1);
        transactionScope.Executor.OwnerCount.ShouldBe(1);
        transactionScope.Executor.CredentialCount.ShouldBe(1);
        transactionScope.Executor.AuditCount.ShouldBe(1);
        transactionScope.Executor.PermissionCount.ShouldBe(0);
    }

    private sealed class UserRow
    {
        public string UserId { get; init; } = string.Empty;

        public string UsernameNormalized { get; init; } = string.Empty;

        public string? DisplayName { get; init; }

        public int IsOwner { get; init; }

        public int IsActive { get; init; }
    }

    private sealed class AuditRow
    {
        public string EventName { get; init; } = string.Empty;

        public string ActorId { get; init; } = string.Empty;

        public string Target { get; init; } = string.Empty;

        public string Result { get; init; } = string.Empty;

        public string DetailsJson { get; init; } = string.Empty;
    }

    private sealed class ThrowingTransactionAuditTrail : IHostTransactionAuditTrail
    {
        public void AppendInScope(IHostPersistenceCommandExecutor executor, AuditEvent auditEvent)
        {
            throw new InvalidOperationException("audit failure");
        }
    }

    private sealed class PausingTransactionScope : IHostPersistenceTransactionScope
    {
        private readonly ManualResetEventSlim releaseFirstTransaction = new(false);
        private int activeTransactions;
        private int transactionCalls;

        public CountingCommandExecutor Executor { get; } = new();

        public TaskCompletionSource FirstTransactionEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaxConcurrentTransactions { get; private set; }

        public void ExecuteInTransaction(Action<IHostPersistenceCommandExecutor> action)
        {
            var active = Interlocked.Increment(ref activeTransactions);
            MaxConcurrentTransactions = Math.Max(MaxConcurrentTransactions, active);
            var call = Interlocked.Increment(ref transactionCalls);
            try
            {
                if (call == 1)
                {
                    FirstTransactionEntered.SetResult();
                    releaseFirstTransaction.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
                }

                action(Executor);
            }
            finally
            {
                Interlocked.Decrement(ref activeTransactions);
            }
        }

        public void ReleaseFirstTransaction()
        {
            releaseFirstTransaction.Set();
        }
    }

    private sealed class CountingCommandExecutor : IHostPersistenceCommandExecutor
    {
        private int ownerCount;
        private int credentialCount;
        private int auditCount;

        public int OwnerCount => ownerCount;

        public int CredentialCount => credentialCount;

        public int AuditCount => auditCount;

        public int PermissionCount { get; private set; }

        public int Execute(string sql, object? parameters = null)
        {
            if (sql.StartsWith("INSERT INTO Users", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref ownerCount);
            }
            else if (sql.StartsWith("INSERT INTO Credentials", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref credentialCount);
            }
            else if (sql.StartsWith("INSERT INTO AuditLog", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref auditCount);
            }
            else if (sql.StartsWith("INSERT INTO Permissions", StringComparison.Ordinal))
            {
                PermissionCount++;
            }

            return 1;
        }

        public IReadOnlyList<T> Query<T>(string sql, object? parameters = null)
        {
            return [];
        }

        public T QueryScalar<T>(string sql, object? parameters = null)
        {
            if (sql == "SELECT COUNT(*) FROM Users;")
            {
                return (T)(object)ownerCount;
            }

            return default!;
        }
    }

    private sealed class OwnerBootstrapFixture : IDisposable
    {
        private OwnerBootstrapFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(Factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            var protector = new RecordingProtector();
            var kdfPolicyResolver = new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" });
            CredentialStore = new CredentialStore(Executor, protector, kdfPolicyResolver);
            OwnerBootstrapService = new OwnerBootstrapService(protector, kdfPolicyResolver);
        }

        public string Root { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public CredentialStore CredentialStore { get; }

        public OwnerBootstrapService OwnerBootstrapService { get; }

        public OwnerBootstrapCoordinator CreateCoordinator(IHostTransactionAuditTrail auditTrail)
        {
            return new OwnerBootstrapCoordinator(
                new HostPersistenceTransactionScope(Factory),
                OwnerBootstrapService,
                auditTrail,
                TimeProvider.System);
        }

        public static OwnerBootstrapFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-owner-bootstrap-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new OwnerBootstrapFixture(root);
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

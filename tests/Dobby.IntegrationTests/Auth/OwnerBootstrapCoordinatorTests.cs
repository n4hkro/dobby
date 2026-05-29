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

[Collection(OwnerBootstrapConcurrencyCollection.Name)]
public sealed class OwnerBootstrapCoordinatorTests
{
    [Fact]
    public void Bootstrap_uses_one_transaction_executor_for_security_mutation_and_audit_append()
    {
        var transactionScope = new RecordingTransactionScope();
        var auditTrail = new RecordingTransactionAuditTrail();
        var coordinator = new OwnerBootstrapCoordinator(
            transactionScope,
            new OwnerBootstrapService(new RecordingProtector(), new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" })),
            auditTrail,
            new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z")));

        var result = coordinator.BootstrapFirstOwner("owner", null, "first owner password", CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        transactionScope.ExecuteInTransactionCalls.ShouldBe(1);
        transactionScope.Executor.QueryScalarCountSql.ShouldBe("SELECT COUNT(*) FROM Users;");
        transactionScope.Executor.InsertedUserId.ShouldNotBeNullOrWhiteSpace();
        transactionScope.Executor.CredentialUserId.ShouldBe(transactionScope.Executor.InsertedUserId);
        auditTrail.Executor.ShouldBeSameAs(transactionScope.Executor);
        auditTrail.AuditEvent.ShouldNotBeNull();
        auditTrail.AuditEvent.EventName.ShouldBe(AuditEventName.UserCreate);
        auditTrail.AuditEvent.ActorId.ShouldBe("system");
        auditTrail.AuditEvent.Target.ShouldBe(transactionScope.Executor.InsertedUserId);
        auditTrail.AuditEvent.Result.ShouldBe(AuditResult.Success);
        auditTrail.AuditEvent.Details.ShouldNotBeNull();
        auditTrail.AuditEvent.Details!["composition"].ShouldBe("B04-transaction-aware-host-audit");
    }

    [Fact]
    public async Task Independent_coordinator_instances_do_not_share_a_process_global_bootstrap_gate()
    {
        var firstScope = new PausingTransactionScope(pauseBeforeAction: true);
        var secondScope = new PausingTransactionScope(pauseBeforeAction: false);
        var first = CreateCoordinator(firstScope);
        var second = CreateCoordinator(secondScope);

        var firstTask = Task.Factory.StartNew(
            () => first.BootstrapFirstOwner("owner-one", null, "first owner password", CorrelationId.New()),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await firstScope.TransactionEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var secondTask = Task.Factory.StartNew(
            () => second.BootstrapFirstOwner("owner-two", null, "second owner password", CorrelationId.New()),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var secondEntryOrTimeout = await Task.WhenAny(
            secondScope.TransactionEntered.Task,
            Task.Delay(TimeSpan.FromMilliseconds(500)));
        firstScope.Release();
        await Task.WhenAll(firstTask, secondTask);

        secondEntryOrTimeout.ShouldBe(secondScope.TransactionEntered.Task);
    }

    private static OwnerBootstrapCoordinator CreateCoordinator(IHostPersistenceTransactionScope transactionScope)
    {
        return new OwnerBootstrapCoordinator(
            transactionScope,
            new OwnerBootstrapService(new RecordingProtector(), new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" })),
            new RecordingTransactionAuditTrail(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z")));
    }

    private sealed class RecordingTransactionScope : IHostPersistenceTransactionScope
    {
        public RecordingCommandExecutor Executor { get; } = new();

        public int ExecuteInTransactionCalls { get; private set; }

        public void ExecuteInTransaction(Action<IHostPersistenceCommandExecutor> action)
        {
            ExecuteInTransactionCalls++;
            action(Executor);
        }
    }

    private sealed class RecordingTransactionAuditTrail : IHostTransactionAuditTrail
    {
        public IHostPersistenceCommandExecutor? Executor { get; private set; }

        public AuditEvent? AuditEvent { get; private set; }

        public void AppendInScope(IHostPersistenceCommandExecutor executor, AuditEvent auditEvent)
        {
            Executor = executor;
            AuditEvent = auditEvent;
        }
    }

    private sealed class PausingTransactionScope(bool pauseBeforeAction) : IHostPersistenceTransactionScope
    {
        private readonly ManualResetEventSlim release = new(false);

        public TaskCompletionSource TransactionEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ExecuteInTransaction(Action<IHostPersistenceCommandExecutor> action)
        {
            TransactionEntered.SetResult();
            if (pauseBeforeAction)
            {
                release.Wait(TimeSpan.FromSeconds(30)).ShouldBeTrue();
            }

            action(new RecordingCommandExecutor());
        }

        public void Release()
        {
            release.Set();
        }
    }

    private sealed class RecordingCommandExecutor : IHostPersistenceCommandExecutor
    {
        public string? QueryScalarCountSql { get; private set; }

        public string? InsertedUserId { get; private set; }

        public string? CredentialUserId { get; private set; }

        public int Execute(string sql, object? parameters = null)
        {
            if (sql.StartsWith("INSERT INTO Users", StringComparison.Ordinal))
            {
                InsertedUserId = ReadString(parameters, "UserId");
                return 1;
            }

            if (sql.StartsWith("INSERT INTO Credentials", StringComparison.Ordinal))
            {
                CredentialUserId = ReadString(parameters, "UserId");
                return 1;
            }

            return 1;
        }

        public IReadOnlyList<T> Query<T>(string sql, object? parameters = null)
        {
            return [];
        }

        public T QueryScalar<T>(string sql, object? parameters = null)
        {
            QueryScalarCountSql = sql;
            return (T)(object)0;
        }

        private static string? ReadString(object? parameters, string name)
        {
            return parameters?.GetType().GetProperty(name)?.GetValue(parameters)?.ToString();
        }
    }

    private sealed class RecordingProtector : IProtectedDataProtector
    {
        public byte[] Protect(byte[] plaintext, byte[] entropy) => plaintext.ToArray();

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy) => protectedBlob.ToArray();
    }
}

using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Persistence;

public sealed class HostMutationAtomicityTests
{
    public static TheoryData<string> InjectedFailurePoints => new()
    {
        "state-write",
        "audit-append",
        "outbox-append",
        "commit"
    };

    [Theory]
    [MemberData(nameof(InjectedFailurePoints))]
    public void State_audit_and_outbox_roll_back_together_at_every_failure_point(string failurePoint)
    {
        using var fixture = MutationFixture.Create();
        var injector = new NamedFailureInjector(failurePoint);
        var unitOfWork = fixture.CreateUnitOfWork(injector);

        Should.Throw<SyntheticMutationFailure>(() => unitOfWork.Execute(context =>
        {
            context.Commands.Execute(
                "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('atomic.test', 'value', 'string', 'None', 'test', @NowUtc);",
                new { NowUtc = fixture.Time.GetUtcNow().ToString("O") });
            context.AppendAudit(CreateAudit(fixture.Time));
            context.EnqueueOutbox(CreateMessage());
        }));

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostConfiguration WHERE ConfigKey = 'atomic.test';").ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE EventName = 'test.atomic-mutation';").ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE IdempotencyKey = 'test.atomic-mutation:1';").ShouldBe(0);
    }

    [Fact]
    public void Successful_mutation_commits_state_audit_and_one_idempotent_outbox_message()
    {
        using var fixture = MutationFixture.Create();
        var unitOfWork = fixture.CreateUnitOfWork();

        string? firstOutboxId = null;
        unitOfWork.Execute(context =>
        {
            context.Commands.Execute(
                "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('atomic.test', 'value', 'string', 'None', 'test', @NowUtc);",
                new { NowUtc = fixture.Time.GetUtcNow().ToString("O") });
            context.AppendAudit(CreateAudit(fixture.Time));
            firstOutboxId = context.EnqueueOutbox(CreateMessage());
        });
        var repeatedOutboxId = unitOfWork.Execute(context => context.EnqueueOutbox(CreateMessage()));

        repeatedOutboxId.ShouldBe(firstOutboxId);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostConfiguration WHERE ConfigKey = 'atomic.test';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE EventName = 'test.atomic-mutation';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE IdempotencyKey = 'test.atomic-mutation:1';").ShouldBe(1);
    }

    [Fact]
    public void Reused_idempotency_key_with_different_payload_fails_and_preserves_original()
    {
        using var fixture = MutationFixture.Create();
        var unitOfWork = fixture.CreateUnitOfWork();
        unitOfWork.Execute(context => context.EnqueueOutbox(CreateMessage()));

        Should.Throw<InvalidOperationException>(() => unitOfWork.Execute(context =>
            context.EnqueueOutbox(CreateMessage() with
            {
                Fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["entityId"] = "different" }
            })));

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE IdempotencyKey = 'test.atomic-mutation:1';").ShouldBe(1);
        fixture.Executor.QueryScalar<string>(
            "SELECT PayloadJson FROM HostOutbox WHERE IdempotencyKey = 'test.atomic-mutation:1';")
            .ShouldContain("entity-1");
    }

    [Fact]
    public async Task Outbox_retry_survives_dispatcher_restart_without_duplicate_effect()
    {
        using var fixture = MutationFixture.Create();
        fixture.CreateUnitOfWork().Execute(context => context.EnqueueOutbox(CreateMessage()));
        var ledger = new IdempotentEffectLedger();
        var firstHandler = new FailAfterFirstEffectiveDeliveryHandler(ledger);
        var firstDispatcher = fixture.CreateDispatcher(firstHandler);

        var first = await firstDispatcher.DispatchBatchAsync(CancellationToken.None);
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        var restartedDispatcher = fixture.CreateDispatcher(new SuccessfulIdempotentHandler(ledger));
        var retried = await restartedDispatcher.DispatchBatchAsync(CancellationToken.None);

        first.ShouldBe(new HostOutboxDispatchResult(1, 0, 1));
        retried.ShouldBe(new HostOutboxDispatchResult(1, 1, 0));
        ledger.EffectiveDeliveryCount.ShouldBe(1);
        ledger.InvocationCount.ShouldBe(2);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE CompletedAtUtc IS NOT NULL;").ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT AttemptCount FROM HostOutbox;").ShouldBe(1);
        fixture.Executor.QueryScalar<string?>(
            "SELECT LastErrorCode FROM HostOutbox;").ShouldBeNull();
    }

    [Fact]
    public async Task Dispatcher_cancellation_releases_claim_without_charging_a_failure()
    {
        using var fixture = MutationFixture.Create();
        fixture.CreateUnitOfWork().Execute(context => context.EnqueueOutbox(CreateMessage()));
        using var cancellation = new CancellationTokenSource();
        var dispatcher = fixture.CreateDispatcher(new CancellingHandler(cancellation));

        await Should.ThrowAsync<OperationCanceledException>(() =>
            dispatcher.DispatchBatchAsync(cancellation.Token));

        fixture.Executor.QueryScalar<string?>("SELECT LeaseId FROM HostOutbox;").ShouldBeNull();
        fixture.Executor.QueryScalar<string?>("SELECT LeaseExpiresAtUtc FROM HostOutbox;").ShouldBeNull();
        fixture.Executor.QueryScalar<int>("SELECT AttemptCount FROM HostOutbox;").ShouldBe(0);
        fixture.Executor.QueryScalar<string?>("SELECT CompletedAtUtc FROM HostOutbox;").ShouldBeNull();
    }

    private static HostOutboxMessage CreateMessage()
    {
        return new HostOutboxMessage(
            "test.atomic-mutation:1",
            "test.mutation",
            "refresh",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["entityId"] = "entity-1" });
    }

    private static AuditEvent CreateAudit(TimeProvider timeProvider)
    {
        return new AuditEvent(
            "test.atomic-mutation",
            "test-actor",
            "host",
            AuditResult.Success,
            CorrelationId.New(),
            timeProvider.GetUtcNow());
    }

    private sealed class MutationFixture : IDisposable
    {
        private MutationFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(Factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            Time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T00:00:00Z"));
        }

        public string Root { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public FakeTimeProvider Time { get; }

        public HostMutationUnitOfWork CreateUnitOfWork(IHostPersistenceTransactionFaultInjector? injector = null)
        {
            var scope = new HostPersistenceTransactionScope(Factory, injector);
            return new HostMutationUnitOfWork(
                scope,
                new DurableAuditTrail(Executor),
                new HostTransactionOutbox(Time));
        }

        public HostOutboxDispatcher CreateDispatcher(IHostOutboxMessageHandler handler)
        {
            return new HostOutboxDispatcher(
                new HostPersistenceTransactionScope(Factory),
                new Dictionary<HostOutboxRoute, IHostOutboxMessageHandler>
                {
                    [new HostOutboxRoute("test.mutation", "refresh")] = handler
                },
                Time);
        }

        public static MutationFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-mutation-uow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new MutationFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class NamedFailureInjector(string failurePoint) : IHostPersistenceTransactionFaultInjector
    {
        public void BeforeCommand(int commandNumber, HostPersistenceCommandKind kind, string commandText)
        {
            if ((failurePoint == "state-write" && commandText.Contains("INSERT INTO HostConfiguration", StringComparison.Ordinal)) ||
                (failurePoint == "audit-append" && commandText.Contains("INSERT INTO AuditLog", StringComparison.Ordinal)) ||
                (failurePoint == "outbox-append" && commandText.Contains("INSERT INTO HostOutbox", StringComparison.Ordinal)))
            {
                throw new SyntheticMutationFailure(failurePoint);
            }
        }

        public void BeforeCommit()
        {
            if (failurePoint == "commit")
            {
                throw new SyntheticMutationFailure(failurePoint);
            }
        }
    }

    private sealed class SyntheticMutationFailure(string failurePoint)
        : Exception("Synthetic mutation failure at " + failurePoint);

    private sealed class IdempotentEffectLedger
    {
        private readonly HashSet<string> delivered = new(StringComparer.Ordinal);

        public int InvocationCount { get; private set; }

        public int EffectiveDeliveryCount => delivered.Count;

        public void Deliver(string idempotencyKey)
        {
            InvocationCount++;
            delivered.Add(idempotencyKey);
        }
    }

    private sealed class FailAfterFirstEffectiveDeliveryHandler(IdempotentEffectLedger ledger)
        : IHostOutboxMessageHandler
    {
        public Task DispatchAsync(HostOutboxDelivery delivery, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ledger.Deliver(delivery.IdempotencyKey);
            throw new IOException("Synthetic post-effect failure.");
        }
    }

    private sealed class SuccessfulIdempotentHandler(IdempotentEffectLedger ledger)
        : IHostOutboxMessageHandler
    {
        public Task DispatchAsync(HostOutboxDelivery delivery, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ledger.Deliver(delivery.IdempotencyKey);
            return Task.CompletedTask;
        }
    }

    private sealed class CancellingHandler(CancellationTokenSource source) : IHostOutboxMessageHandler
    {
        public Task DispatchAsync(HostOutboxDelivery delivery, CancellationToken cancellationToken)
        {
            source.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}

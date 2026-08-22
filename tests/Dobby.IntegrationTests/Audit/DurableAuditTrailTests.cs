using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Shouldly;

namespace Dobby.IntegrationTests.Audit;

public sealed class DurableAuditTrailTests
{
    [Fact]
    public void Durable_audit_trail_round_trips_sanitized_append_only_events()
    {
        using var fixture = AuditPersistenceFixture.Create();
        IAuditTrail trail = new DurableAuditTrail(fixture.Executor);
        var auditEvent = new AuditEvent(
            AuditEventName.UserLoginFailed,
            "owner",
            "host",
            AuditResult.Failed,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { ["reason"] = "accepted" });

        trail.Append(auditEvent);

        var snapshot = trail.Snapshot();
        snapshot.Count.ShouldBe(1);
        snapshot[0].EventName.ShouldBe(auditEvent.EventName);
        snapshot[0].ActorId.ShouldBe(auditEvent.ActorId);
        snapshot[0].Details!["reason"].ShouldBe("accepted");
    }

    [Fact]
    public void Durable_audit_round_trip_contract_does_not_include_severity()
    {
        using var fixture = AuditPersistenceFixture.Create();
        IAuditTrail trail = new DurableAuditTrail(fixture.Executor);
        var auditEvent = new AuditEvent(
            AuditEventName.UserLoginFailed,
            "owner",
            "host",
            AuditResult.Failed,
            CorrelationId.New(),
            DateTimeOffset.Parse("2026-05-13T08:00:00Z"),
            new Dictionary<string, string> { ["reason"] = "invalid-password" });

        trail.Append(auditEvent);

        fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('AuditLog') ORDER BY cid;")
            .ShouldBe([
                "AuditId",
                "OccurredAtUtc",
                "EventName",
                "ActorId",
                "Target",
                "Result",
                "CorrelationId",
                "DetailsJson"
            ]);
        var snapshotEvent = trail.Snapshot().Single();
        var payload = snapshotEvent.ToCanonicalPayload();
        payload.Keys.ShouldBe([
            "timestamp",
            "actor",
            "action",
            "target",
            "result",
            "correlationId",
            "details"
        ]);
        payload.Keys.Any(key => string.Equals(key, "severity", StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse();
        typeof(AuditEvent).GetProperty("Severity").ShouldBeNull();
    }

    [Fact]
    public void Durable_audit_trail_reuses_sanitizer_for_secret_details()
    {
        using var fixture = AuditPersistenceFixture.Create();
        IAuditTrail trail = new DurableAuditTrail(fixture.Executor);
        var auditEvent = new AuditEvent(
            AuditEventName.UserLoginFailed,
            "owner",
            "host",
            AuditResult.Failed,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { ["password"] = "raw" });

        Should.Throw<InvalidOperationException>(() => trail.Append(auditEvent));
    }

    [Fact]
    public void Transaction_aware_audit_append_uses_caller_scope_and_same_sanitized_row_shape()
    {
        using var fixture = AuditPersistenceFixture.Create();
        var scope = new HostPersistenceTransactionScope(fixture.Factory);
        IHostTransactionAuditTrail transactionTrail = new DurableAuditTrail(fixture.Executor);
        var auditEvent = new AuditEvent(
            AuditEventName.UserCreate,
            "system",
            "user-1",
            AuditResult.Success,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>
            {
                ["bootstrapKind"] = "first-user-owner",
                ["principalRole"] = "Owner",
                ["permissionState"] = "is-owner-short-circuit",
                ["composition"] = "B04-transaction-aware-host-audit"
            });

        scope.ExecuteInTransaction(executor => transactionTrail.AppendInScope(executor, auditEvent));

        var snapshot = new DurableAuditTrail(fixture.Executor).Snapshot();
        snapshot.Count.ShouldBe(1);
        snapshot[0].EventName.ShouldBe(AuditEventName.UserCreate);
        var actualDetails = snapshot[0].Details ?? throw new InvalidOperationException("Audit details were not persisted.");
        actualDetails.Count.ShouldBe(auditEvent.Details!.Count);
        foreach (var detail in auditEvent.Details)
        {
            actualDetails[detail.Key].ShouldBe(detail.Value);
        }
    }

    [Fact]
    public void Transaction_aware_audit_append_rolls_back_with_caller_transaction()
    {
        using var fixture = AuditPersistenceFixture.Create();
        var scope = new HostPersistenceTransactionScope(fixture.Factory);
        IHostTransactionAuditTrail transactionTrail = new DurableAuditTrail(fixture.Executor);
        var auditEvent = new AuditEvent(
            AuditEventName.UserCreate,
            "system",
            "user-1",
            AuditResult.Success,
            CorrelationId.New(),
            DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() => scope.ExecuteInTransaction(executor =>
        {
            transactionTrail.AppendInScope(executor, auditEvent);
            throw new InvalidOperationException("simulate owner bootstrap failure");
        }));

        new DurableAuditTrail(fixture.Executor).Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Audit_retention_sweeper_removes_only_rows_older_than_floor()
    {
        using var fixture = AuditPersistenceFixture.Create();
        var old = DateTimeOffset.UtcNow.AddDays(-366).ToString("O");
        var retained = DateTimeOffset.UtcNow.AddDays(-364).ToString("O");
        fixture.Executor.Execute(
            "INSERT INTO AuditLog(AuditId, OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson) VALUES ('old', @Old, 'audit.old', 'system', 'audit', 'success', @CorrelationId, '{}');",
            new { Old = old, CorrelationId = CorrelationId.New().ToString() });
        fixture.Executor.Execute(
            "INSERT INTO AuditLog(AuditId, OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson) VALUES ('retained', @Retained, 'audit.retained', 'system', 'audit', 'success', @CorrelationId, '{}');",
            new { Retained = retained, CorrelationId = CorrelationId.New().ToString() });

        var sweeper = new AuditRetentionSweeper(fixture.Executor, retentionDays: 365);
        sweeper.Sweep();

        fixture.Executor.Query<string>("SELECT AuditId FROM AuditLog ORDER BY AuditId;")
            .ShouldBe(["retained"]);
    }

    private sealed class AuditPersistenceFixture : IDisposable
    {
        private AuditPersistenceFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            var migrator = new HostPersistenceMigrator(factory, probe);
            migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Factory = factory;
            Executor = new HostPersistenceCommandExecutor(factory);
        }

        public string Root { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public static AuditPersistenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-audit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new AuditPersistenceFixture(root);
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

using System.Security.Cryptography;
using System.Text.Json;
using Dobby.Audit;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Dobby.Security.Trust;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Recovery;

public sealed class RecoveryRequestExportTests
{
    [Fact]
    public void Export_signs_v2_request_persists_binding_and_appends_audit_and_outbox_atomically()
    {
        using var fixture = RecoveryRequestFixture.Create();
        using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T00:00:00Z"));
        var signer = new DeviceRecoveryKeySigner(
            deviceKey.ExportPkcs8PrivateKey(),
            deviceKey.ExportSubjectPublicKeyInfo());
        var exporter = new RecoveryRequestExportCoordinator(
            new RecoveryRequestExporter(
                signer,
                new RecoveryAuthorityContext("customer-1", "deployment-1", "device:stable-1"),
                time,
                VendorRecoveryPublicKey.DevQaKeyId),
            new HostMutationUnitOfWork(
                fixture.TransactionScope,
                new DurableAuditTrail(fixture.Executor),
                new HostTransactionOutbox(time)),
            time);

        var result = exporter.Export(fixture.Root, CorrelationId.New());

        result.IsSuccess.ShouldBeTrue(result.ErrorCode?.ToString());
        var json = File.ReadAllText(result.Value.FilePath);
        json.ShouldNotContain("machineGuid", Case.Insensitive);
        json.ShouldNotContain("private", Case.Insensitive);
        var envelope = RecoveryRequestVerifier.ReadEnvelope(json);
        new RecoveryRequestVerifier().Verify(
            envelope,
            deviceKey.ExportSubjectPublicKeyInfo(),
            time.GetUtcNow()).IsSuccess.ShouldBeTrue();
        envelope.Payload.SchemaVersion.ShouldBe(2);
        envelope.Payload.RecoveryPublicKeyHash.ShouldBe(signer.PublicKeyHash);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RecoveryRequests WHERE RequestId = @RequestId;", new { envelope.Payload.RequestId }).ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'recovery.export' AND Result = 'success';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM HostOutbox WHERE MessageType = 'recovery.requested';").ShouldBe(1);
    }

    [Fact]
    public void Export_commit_failure_removes_artifact_and_rolls_back_request_audit_and_outbox()
    {
        using var fixture = RecoveryRequestFixture.Create();
        using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T00:00:00Z"));
        var coordinator = new RecoveryRequestExportCoordinator(
            new RecoveryRequestExporter(
                new DeviceRecoveryKeySigner(
                    deviceKey.ExportPkcs8PrivateKey(),
                    deviceKey.ExportSubjectPublicKeyInfo()),
                new RecoveryAuthorityContext("customer-1", "deployment-1", "device:stable-1"),
                time,
                VendorRecoveryPublicKey.DevQaKeyId),
            new HostMutationUnitOfWork(
                new HostPersistenceTransactionScope(fixture.Factory, new CommitFailureInjector()),
                new DurableAuditTrail(fixture.Executor),
                new HostTransactionOutbox(time)),
            time);

        var result = coordinator.Export(fixture.Root, CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        Directory.EnumerateFiles(fixture.Root, "*.dobbyrecoveryrequest").ShouldBeEmpty();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RecoveryRequests;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'recovery.export';").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM HostOutbox;").ShouldBe(0);
    }

    private sealed class RecoveryRequestFixture : IDisposable
    {
        private RecoveryRequestFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(Factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            TransactionScope = new HostPersistenceTransactionScope(Factory);
        }

        public string Root { get; }
        public EncryptedSqliteConnectionFactory Factory { get; }
        public HostPersistenceCommandExecutor Executor { get; }
        public HostPersistenceTransactionScope TransactionScope { get; }

        public static RecoveryRequestFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-recovery-request-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new RecoveryRequestFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class CommitFailureInjector : IHostPersistenceTransactionFaultInjector
    {
        public void BeforeCommand(int commandNumber, HostPersistenceCommandKind kind, string commandText)
        {
        }

        public void BeforeCommit()
        {
            throw new InvalidOperationException("Synthetic recovery request commit failure.");
        }
    }
}

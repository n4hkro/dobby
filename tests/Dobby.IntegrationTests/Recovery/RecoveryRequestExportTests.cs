using System.Text.Json;
using Dobby.Audit;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Recovery;

public sealed class RecoveryRequestExportTests
{
    [Fact]
    public void Export_writes_unsigned_recovery_request_and_appends_audit_row()
    {
        using var fixture = RecoveryRequestFixture.Create();
        var device = new DeviceFingerprint("machine", "cpu", "board", "disk", "mac");
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var exporter = new RecoveryRequestExportCoordinator(
            new RecoveryRequestExporter(
                new StaticDeviceFingerprint(device),
                new FixedHostVersionProvider(new HostVersion(1, 2, 3)),
                time,
                "Owner laptop"),
            new DurableAuditTrail(fixture.Executor));

        var result = exporter.Export(fixture.Root, CorrelationId.New());

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.ErrorCode.ToString());
        Path.GetExtension(result.Value.FilePath).ShouldBe(".dobbyrecoveryrequest");
        var json = File.ReadAllText(result.Value.FilePath);
        json.ShouldNotContain("signature", Case.Insensitive);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("type").GetString().ShouldBe("dobby.owner-recovery-request.v1");
        root.GetProperty("dobbyVersion").GetString().ShouldBe("1.2.3");
        root.GetProperty("requestedAt").GetString().ShouldBe(time.GetUtcNow().ToString("O"));
        root.GetProperty("deviceDisplayName").GetString().ShouldBe("Owner laptop");
        Guid.TryParse(root.GetProperty("requestId").GetString(), out _).ShouldBeTrue();
        root.GetProperty("deviceIdentity").GetProperty("machineGuid").GetString().ShouldBe("machine");
        root.GetProperty("deviceIdentity").GetProperty("cpuId").GetString().ShouldBe("cpu");
        root.GetProperty("deviceIdentity").GetProperty("motherboardSerial").GetString().ShouldBe("board");
        root.GetProperty("deviceIdentity").GetProperty("diskSerial").GetString().ShouldBe("disk");
        root.GetProperty("deviceIdentity").GetProperty("macAddress").GetString().ShouldBe("mac");
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'recovery.export' AND Result = 'success';").ShouldBe(1);
    }

    private sealed class StaticDeviceFingerprint(DeviceFingerprint fingerprint) : IDeviceFingerprintSource
    {
        public Result<DeviceFingerprint> Current() => Result.Success(fingerprint);
    }

    private sealed class RecoveryRequestFixture : IDisposable
    {
        private RecoveryRequestFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
        }

        public string Root { get; }

        public HostPersistenceCommandExecutor Executor { get; }

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
}

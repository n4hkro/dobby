using Dobby.Audit;
using Dobby.Core;
using Dobby.Host;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Licensing;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Licensing;

public sealed class LicenseImportAtomicityTests
{
    public static TheoryData<string> FailurePoints => new()
    {
        "state",
        "attestation",
        "audit",
        "outbox",
        "commit"
    };

    [Theory]
    [MemberData(nameof(FailurePoints))]
    public void Authoritative_license_import_effects_roll_back_together(string failurePoint)
    {
        using var fixture = Fixture.Create();
        var store = fixture.CreateStore(new FailureInjector(failurePoint));

        Should.Throw<SyntheticLicenseImportFailure>(() => store.CommitSuccessfulImport(
            CreateState(fixture.Time),
            CreateAttestation(fixture.Time),
            CreateMetadata()));

        fixture.Count("PluginLicenseState").ShouldBe(0);
        fixture.Count("PluginLicenseImportAttestation").ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE EventName = @EventName;",
            new { EventName = AuditEventName.LicenseImportSucceeded }).ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE Purpose = 'license';").ShouldBe(0);
    }

    [Fact]
    public void Successful_import_commits_once_and_exact_retry_is_idempotent()
    {
        using var fixture = Fixture.Create();
        var store = fixture.CreateStore();
        var state = CreateState(fixture.Time);

        var first = store.CommitSuccessfulImport(state, CreateAttestation(fixture.Time), CreateMetadata());
        var retry = store.CommitSuccessfulImport(
            state with { ImportedAtUtc = fixture.Time.GetUtcNow().AddMinutes(1) },
            CreateAttestation(fixture.Time),
            CreateMetadata());

        first.ShouldBe(new LicenseImportCommitResult(
            LicenseImportCommitOutcome.Committed,
            SuccessAuditAlreadyPresent: true));
        retry.ShouldBe(new LicenseImportCommitResult(
            LicenseImportCommitOutcome.AlreadyCurrent,
            SuccessAuditAlreadyPresent: true));
        fixture.Count("PluginLicenseState").ShouldBe(1);
        fixture.Count("PluginLicenseImportAttestation").ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE EventName = @EventName;",
            new { EventName = AuditEventName.LicenseImportSucceeded }).ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE IdempotencyKey = 'license-import:license-1:7';")
            .ShouldBe(1);
    }

    [Fact]
    public void Older_or_conflicting_revision_cannot_replace_current_state()
    {
        using var fixture = Fixture.Create();
        var store = fixture.CreateStore();
        var current = CreateState(fixture.Time) with { LicenseRevision = 8, LicenseId = "license-2" };
        store.CommitSuccessfulImport(current, CreateAttestation(fixture.Time, 8), CreateMetadata());

        var older = store.CommitSuccessfulImport(
            CreateState(fixture.Time),
            CreateAttestation(fixture.Time),
            CreateMetadata());
        var conflicting = store.CommitSuccessfulImport(
            current with { LicenseId = "different-license" },
            CreateAttestation(fixture.Time, 8),
            CreateMetadata());

        older.Outcome.ShouldBe(LicenseImportCommitOutcome.OlderOrConflictingRevision);
        conflicting.Outcome.ShouldBe(LicenseImportCommitOutcome.OlderOrConflictingRevision);
        store.Read(current.RevisionSeriesKey)!.LicenseRevision.ShouldBe(8);
        fixture.Count("PluginLicenseImportAttestation").ShouldBe(1);
    }

    private static LicenseStateRecord CreateState(FakeTimeProvider time)
    {
        var now = time.GetUtcNow();
        return new LicenseStateRecord(
            new RevisionSeriesKey("plugin.sample", "default", "default"),
            "plugin.sample",
            "default",
            "default",
            new string('a', 64),
            new string('b', 64),
            7,
            LicenseLifecycleState.Active,
            now,
            now.AddDays(-1),
            now.AddDays(30),
            3,
            SourceStatus: null,
            LicenseId: "license-1",
            RevisionSeriesId: "series-1",
            KeyId: "license-prod-2026-01",
            IssuedAtUtc: now.AddDays(-1),
            SourceIntent: LicenseGrantIntent.Granted,
            TrustedTimeHealth: TrustedTimeHealth.Healthy);
    }

    private static LicenseImportAttestation CreateAttestation(FakeTimeProvider time, int revision = 7)
    {
        return new LicenseImportAttestation(
            new string('c', 64),
            revision,
            time.GetUtcNow(),
            "succeeded",
            "11111111-1111-1111-1111-111111111111");
    }

    private static LicenseImportCommitMetadata CreateMetadata()
    {
        return new LicenseImportCommitMetadata(
            "owner-1",
            "11111111-1111-1111-1111-111111111111");
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            var migrator = new HostPersistenceMigrator(Factory, probe);
            migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            Time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T00:00:00Z"));
        }

        public string Root { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public FakeTimeProvider Time { get; }

        public HostLicenseStateStore CreateStore(IHostPersistenceTransactionFaultInjector? injector = null)
        {
            var transactionScope = new HostPersistenceTransactionScope(Factory, injector);
            var unitOfWork = new HostMutationUnitOfWork(
                transactionScope,
                new DurableAuditTrail(Executor),
                new HostTransactionOutbox(Time));
            return new HostLicenseStateStore(Executor, transactionScope, unitOfWork);
        }

        public int Count(string tableName)
        {
            return tableName switch
            {
                "PluginLicenseState" => Executor.QueryScalar<int>("SELECT COUNT(*) FROM PluginLicenseState;"),
                "PluginLicenseImportAttestation" => Executor.QueryScalar<int>("SELECT COUNT(*) FROM PluginLicenseImportAttestation;"),
                _ => throw new ArgumentOutOfRangeException(nameof(tableName))
            };
        }

        public static Fixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-license-atomicity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new Fixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class FailureInjector(string failurePoint) : IHostPersistenceTransactionFaultInjector
    {
        public void BeforeCommand(int commandNumber, HostPersistenceCommandKind kind, string commandText)
        {
            if ((failurePoint == "state" && commandText.Contains("INSERT INTO PluginLicenseState", StringComparison.Ordinal)) ||
                (failurePoint == "attestation" && commandText.Contains("INSERT INTO PluginLicenseImportAttestation", StringComparison.Ordinal)) ||
                (failurePoint == "audit" && commandText.Contains("INSERT INTO AuditLog", StringComparison.Ordinal)) ||
                (failurePoint == "outbox" && commandText.Contains("INSERT INTO HostOutbox", StringComparison.Ordinal)))
            {
                throw new SyntheticLicenseImportFailure(failurePoint);
            }
        }

        public void BeforeCommit()
        {
            if (failurePoint == "commit")
            {
                throw new SyntheticLicenseImportFailure(failurePoint);
            }
        }
    }

    private sealed class SyntheticLicenseImportFailure(string failurePoint)
        : Exception("Synthetic license import failure at " + failurePoint);
}

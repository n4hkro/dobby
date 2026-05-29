using System.Security.Cryptography;
using System.Text.Json;
using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Recovery;

public sealed class RecoveryResponseImportFixtureTests
{
    [Fact]
    public void Signature_ok_fixture_confirm_path_rebinds_password_burns_nonce_invalidates_session_and_audits()
    {
        using var fixture = FixtureContext.Create();
        var ownerId = fixture.InsertOwnerWithCredential("old password");
        fixture.InsertActiveSession(ownerId, "session-1");
        var coordinator = fixture.CreateCoordinator();
        var challenge = coordinator.Verify(ReadFixture("signature-ok")).Value;

        coordinator.ConfirmPasswordReset(challenge, "new password", CorrelationId.New()).IsSuccess.ShouldBeTrue();

        fixture.CredentialStore.VerifyPassword(ownerId, "old password").Value.ShouldBeFalse();
        fixture.CredentialStore.VerifyPassword(ownerId, "new password").Value.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces;").ShouldBe(1);
        fixture.Executor.QueryScalar<string>("SELECT InvalidationReason FROM Sessions WHERE SessionId = 'session-1';").ShouldBe("owner-recovery");
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'recovery.import' AND Result = 'success';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'session.invalidated' AND Result = 'success';").ShouldBe(1);
    }

    [Theory]
    [InlineData("signature-fail")]
    [InlineData("expired")]
    [InlineData("device-mismatch")]
    public void Invalid_response_fixtures_fail_closed_before_password_reset(string fixtureName)
    {
        using var fixture = FixtureContext.Create();
        fixture.InsertOwnerWithCredential("old password");

        var result = fixture.CreateCoordinator().Verify(ReadFixture(fixtureName));

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        fixture.AssertRecoveryImportAudit(fixtureName, envelope: ReadFixture(fixtureName));
    }

    [Fact]
    public void Replay_fixture_fails_closed_when_nonce_is_already_consumed()
    {
        using var fixture = FixtureContext.Create();
        fixture.InsertOwnerWithCredential("old password");
        var envelope = ReadFixture("replay");
        fixture.InsertConsumedNonce(envelope.Payload.Nonce, envelope.Payload.RequestId);

        var result = fixture.CreateCoordinator().Verify(envelope);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        fixture.AssertRecoveryImportAudit("replay", envelope);
    }

    [Fact]
    public void Owner_missing_fixture_fails_closed_before_password_reset()
    {
        using var fixture = FixtureContext.Create();

        var result = fixture.CreateCoordinator().Verify(ReadFixture("owner-missing"));

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        fixture.AssertRecoveryImportAudit("owner-missing", ReadFixture("owner-missing"));
    }

    [Fact]
    public void Cancel_fixture_leaves_nonce_credentials_and_session_unchanged()
    {
        using var fixture = FixtureContext.Create();
        var ownerId = fixture.InsertOwnerWithCredential("old password");
        fixture.InsertActiveSession(ownerId, "session-1");
        var coordinator = fixture.CreateCoordinator();
        var challenge = coordinator.Verify(ReadFixture("cancel")).Value;

        var result = coordinator.Cancel(challenge, CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
        fixture.CredentialStore.VerifyPassword(ownerId, "old password").Value.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE InvalidatedAtUtc IS NOT NULL;").ShouldBe(0);
    }

    private static RecoveryResponseEnvelope ReadFixture(string name)
    {
        return RecoveryResponseImporter.ReadEnvelope(File.ReadAllText(Path.Combine(FixtureDirectory(), name + ".TEST-ONLY.dobbyrecovery")));
    }

    private static string PublicKeyText()
    {
        return File.ReadAllText(Path.Combine(FixtureDirectory(), "vendor-recovery-public-key.TEST-ONLY.txt"));
    }

    private static string FixtureDirectory()
    {
        return Path.Combine(RepoRoot().FullName, "tests", "Dobby.IntegrationTests", "Recovery", "Fixtures", "TEST-ONLY");
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }

    private sealed class FixtureContext : IDisposable
    {
        private readonly RecordingProtector protector = new();
        private readonly KdfPolicyResolver kdfPolicyResolver = new(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" });
        private readonly FakeTimeProvider time = new(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));

        private FixtureContext(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(Factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            CredentialStore = new CredentialStore(Executor, protector, kdfPolicyResolver);
        }

        public string Root { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public CredentialStore CredentialStore { get; }

        public string InsertOwnerWithCredential(string password)
        {
            var now = time.GetUtcNow();
            var ownerId = Guid.CreateVersion7().ToString("D");
            Executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
                new { UserId = ownerId, Now = now.ToString("O") });
            CredentialStore.CreateCredential(ownerId, password, now).IsSuccess.ShouldBeTrue();
            return ownerId;
        }

        public void InsertActiveSession(string ownerId, string sessionId)
        {
            var now = time.GetUtcNow();
            Executor.Execute(
                "INSERT INTO Sessions(SessionId, UserId, CreatedAtUtc, LastActivityAtUtc, ExpiresAtUtc, InvalidatedAtUtc, InvalidationReason, SessionVersion) VALUES (@SessionId, @UserId, @Now, @Now, @ExpiresAtUtc, NULL, NULL, 1);",
                new { SessionId = sessionId, UserId = ownerId, Now = now.ToString("O"), ExpiresAtUtc = now.AddHours(8).ToString("O") });
        }

        public void InsertConsumedNonce(string nonce, string requestId)
        {
            Executor.Execute(
                "INSERT INTO ConsumedRecoveryNonces(NonceHash, ConsumedAtUtc, RequestId) VALUES (@NonceHash, @ConsumedAtUtc, @RequestId);",
                new { NonceHash = RecoveryResponseImportCoordinator.HashNonce(nonce), ConsumedAtUtc = time.GetUtcNow().ToString("O"), RequestId = requestId });
        }

        public void AssertRecoveryImportAudit(string outcome, RecoveryResponseEnvelope envelope)
        {
            var audit = Executor.Query<AuditRow>("SELECT Result, DetailsJson FROM AuditLog WHERE EventName = 'recovery.import';").Single();
            audit.Result.ShouldBe("failed");
            var details = JsonSerializer.Deserialize<Dictionary<string, string>>(audit.DetailsJson)!;
            details["outcome"].ShouldBe(outcome);
            details["requestId"].ShouldBe(envelope.Payload.RequestId);
            details["nonceHash"].ShouldBe(RecoveryResponseImportCoordinator.HashNonce(envelope.Payload.Nonce));
            details.ShouldNotContainKey("nonce");
        }

        public RecoveryResponseImportCoordinator CreateCoordinator()
        {
            var importer = new RecoveryResponseImporter(
                new PemVendorPublicKey(PublicKeyText()),
                new StaticDeviceFingerprint(new DeviceFingerprint("a", "b", "c", "d", "e")));
            return new RecoveryResponseImportCoordinator(
                new HostPersistenceTransactionScope(Factory),
                Executor,
                importer,
                protector,
                kdfPolicyResolver,
                new DurableAuditTrail(Executor),
                time);
        }

        public static FixtureContext Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-recovery-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new FixtureContext(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class AuditRow
    {
        public string Result { get; init; } = string.Empty;

        public string DetailsJson { get; init; } = string.Empty;
    }

    private sealed class StaticDeviceFingerprint(DeviceFingerprint fingerprint) : IDeviceFingerprintSource
    {
        public Result<DeviceFingerprint> Current() => Result.Success(fingerprint);
    }

    private sealed class PemVendorPublicKey : IVendorRecoveryPublicKey, IDisposable
    {
        private readonly ECDsa key = ECDsa.Create();

        public PemVendorPublicKey(string pem)
        {
            key.ImportFromPem(pem);
        }

        public bool Verify(byte[] canonicalPayload, byte[] signature)
        {
            return key.VerifyData(canonicalPayload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }

        public void Dispose()
        {
            key.Dispose();
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

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

public sealed class RecoveryResponseImportConfirmTests
{
    [Fact]
    public void Confirm_rebinds_owner_password_burns_nonce_invalidates_owner_sessions_and_audits_atomically()
    {
        using var fixture = RecoveryImportFixture.Create();
        var ownerId = fixture.InsertOwnerWithCredential("old password");
        fixture.InsertActiveSession(ownerId, "session-1");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = fixture.Payload("nonce-1");
        var envelope = fixture.Envelope(key, payload);
        var coordinator = fixture.CreateCoordinator(key);
        var challenge = coordinator.Verify(envelope).Value;

        var result = coordinator.ConfirmPasswordReset(challenge, "new password", CorrelationId.New());

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.ErrorCode.ToString());
        fixture.CredentialStore.VerifyPassword(ownerId, "old password").Value.ShouldBeFalse();
        fixture.CredentialStore.VerifyPassword(ownerId, "new password").Value.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces;").ShouldBe(1);
        fixture.Executor.QueryScalar<string>("SELECT InvalidationReason FROM Sessions WHERE SessionId = 'session-1';").ShouldBe("owner-recovery");
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'recovery.import' AND Result = 'success';").ShouldBe(1);
        var detailsJson = fixture.Executor.QueryScalar<string>("SELECT DetailsJson FROM AuditLog WHERE EventName = 'recovery.import';");
        var details = JsonSerializer.Deserialize<Dictionary<string, string>>(detailsJson)!;
        details["outcome"].ShouldBe("success");
        details["nonceHash"].ShouldBe(RecoveryResponseImportCoordinator.HashNonce(payload.Nonce));
        details.ShouldNotContainKey("nonce");
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'session.invalidated' AND Result = 'success';").ShouldBe(1);
    }

    [Fact]
    public void Cancel_after_verified_challenge_leaves_nonce_credentials_and_sessions_unchanged()
    {
        using var fixture = RecoveryImportFixture.Create();
        var ownerId = fixture.InsertOwnerWithCredential("old password");
        fixture.InsertActiveSession(ownerId, "session-1");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var coordinator = fixture.CreateCoordinator(key);
        var challenge = coordinator.Verify(fixture.Envelope(key, fixture.Payload("nonce-1"))).Value;

        var result = coordinator.Cancel(challenge, CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
        fixture.CredentialStore.VerifyPassword(ownerId, "old password").Value.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE InvalidatedAtUtc IS NOT NULL;").ShouldBe(0);
        var audit = fixture.Executor.Query<AuditRow>("SELECT Result, DetailsJson FROM AuditLog WHERE EventName = 'recovery.import';").Single();
        audit.Result.ShouldBe("failed");
        var details = JsonSerializer.Deserialize<Dictionary<string, string>>(audit.DetailsJson)!;
        details["outcome"].ShouldBe("cancelled");
        details["nonceHash"].ShouldBe(RecoveryResponseImportCoordinator.HashNonce(challenge.Payload.Nonce));
        details.ShouldNotContainKey("nonce");
    }

    [Fact]
    public void Confirm_fails_closed_when_verified_recovery_response_expires_before_password_reset()
    {
        using var fixture = RecoveryImportFixture.Create();
        var ownerId = fixture.InsertOwnerWithCredential("old password");
        fixture.InsertActiveSession(ownerId, "session-1");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var coordinator = fixture.CreateCoordinator(key);
        var challenge = coordinator.Verify(fixture.Envelope(key, fixture.Payload("nonce-1"))).Value;
        fixture.Advance(TimeSpan.FromHours(25));

        var result = coordinator.ConfirmPasswordReset(challenge, "new password", CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        fixture.CredentialStore.VerifyPassword(ownerId, "old password").Value.ShouldBeTrue();
        fixture.CredentialStore.VerifyPassword(ownerId, "new password").Value.ShouldBeFalse();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE InvalidatedAtUtc IS NOT NULL;").ShouldBe(0);
        var audit = fixture.Executor.Query<AuditRow>("SELECT Result, DetailsJson FROM AuditLog WHERE EventName = 'recovery.import';").Single();
        audit.Result.ShouldBe("failed");
        var details = JsonSerializer.Deserialize<Dictionary<string, string>>(audit.DetailsJson)!;
        details["outcome"].ShouldBe("expired");
        details["nonceHash"].ShouldBe(RecoveryResponseImportCoordinator.HashNonce(challenge.Payload.Nonce));
    }

    [Fact]
    public void Confirm_rolls_back_credential_nonce_session_and_audit_when_transaction_audit_fails()
    {
        using var fixture = RecoveryImportFixture.Create();
        var ownerId = fixture.InsertOwnerWithCredential("old password");
        fixture.InsertActiveSession(ownerId, "session-1");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var coordinator = fixture.CreateCoordinator(key, new ThrowingTransactionAuditTrail());
        var challenge = coordinator.Verify(fixture.Envelope(key, fixture.Payload("nonce-1"))).Value;

        var result = coordinator.ConfirmPasswordReset(challenge, "new password", CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        fixture.CredentialStore.VerifyPassword(ownerId, "old password").Value.ShouldBeTrue();
        fixture.CredentialStore.VerifyPassword(ownerId, "new password").Value.ShouldBeFalse();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE SessionId = 'session-1' AND InvalidatedAtUtc IS NULL AND InvalidationReason IS NULL;").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog;").ShouldBe(0);
    }

    [Fact]
    public void Verify_fails_closed_when_nonce_was_already_consumed_or_owner_missing()
    {
        using var fixture = RecoveryImportFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var coordinator = fixture.CreateCoordinator(key);
        var payload = fixture.Payload("nonce-1");
        fixture.InsertConsumedNonce(payload.Nonce, payload.RequestId);

        coordinator.Verify(fixture.Envelope(key, payload)).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        fixture.Executor.Execute("DELETE FROM ConsumedRecoveryNonces;");
        coordinator.Verify(fixture.Envelope(key, fixture.Payload("nonce-2"))).ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    private sealed class RecoveryImportFixture : IDisposable
    {
        private readonly RecordingProtector protector = new();
        private readonly KdfPolicyResolver kdfPolicyResolver = new(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" });
        private readonly FakeTimeProvider time = new(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));

        private RecoveryImportFixture(string root)
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

        public void Advance(TimeSpan duration)
        {
            time.Advance(duration);
        }

        public RecoveryResponsePayload Payload(string nonce)
        {
            return new RecoveryResponsePayload(
                "dobby.owner-recovery-response.v1",
                Guid.NewGuid().ToString("D"),
                new RecoveryDeviceIdentity("a", "b", "c", "d", "e"),
                "2026-05-18T00:00:00Z",
                "2026-05-19T00:00:00Z",
                nonce,
                1);
        }

        public RecoveryResponseEnvelope Envelope(ECDsa key, RecoveryResponsePayload payload)
        {
            var signature = key.SignData(RecoveryCanonicalJsonWriter.WritePayload(payload), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return new RecoveryResponseEnvelope(payload, Convert.ToBase64String(signature));
        }

        public RecoveryResponseImportCoordinator CreateCoordinator(ECDsa key)
        {
            return CreateCoordinator(key, new DurableAuditTrail(Executor));
        }

        public RecoveryResponseImportCoordinator CreateCoordinator(ECDsa key, IHostTransactionAuditTrail auditTrail)
        {
            var importer = new RecoveryResponseImporter(
                new EcdsaVendorPublicKey(key),
                new StaticDeviceFingerprint(new DeviceFingerprint("a", "b", "c", "d", "e")));
            return new RecoveryResponseImportCoordinator(
                new HostPersistenceTransactionScope(Factory),
                Executor,
                importer,
                protector,
                kdfPolicyResolver,
                auditTrail,
                time);
        }

        public static RecoveryImportFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-recovery-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new RecoveryImportFixture(root);
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

    private sealed class EcdsaVendorPublicKey(ECDsa key) : IVendorRecoveryPublicKey
    {
        public bool Verify(byte[] canonicalPayload, byte[] signature)
        {
            return key.VerifyData(canonicalPayload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
    }

    private sealed class ThrowingTransactionAuditTrail : IHostTransactionAuditTrail
    {
        public void AppendInScope(IHostPersistenceCommandExecutor executor, AuditEvent auditEvent)
        {
            throw new InvalidOperationException("Injected recovery audit failure.");
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

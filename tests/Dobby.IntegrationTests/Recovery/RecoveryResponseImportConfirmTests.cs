using System.Security.Cryptography;
using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Dobby.Security.Trust;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Recovery;

public sealed class RecoveryResponseImportConfirmTests
{
    [Fact]
    public void Confirm_atomically_rebinds_owner_consumes_request_invalidates_sessions_and_emits_evidence()
    {
        using var fixture = RecoveryImportFixture.Create();
        var ownerId = fixture.InsertOwnerWithCredential("old password");
        fixture.InsertActiveSession(ownerId, "session-1");
        var envelope = fixture.CreateStoredResponse();
        var coordinator = fixture.CreateCoordinator();
        var challenge = coordinator.Verify(envelope).Value;

        var result = coordinator.ConfirmPasswordReset(
            challenge,
            "new owner password",
            CorrelationId.New());

        result.IsSuccess.ShouldBeTrue(result.ErrorCode?.ToString());
        fixture.CredentialStore.VerifyPassword(ownerId, "old password").Value.ShouldBeFalse();
        fixture.CredentialStore.VerifyPassword(ownerId, "new owner password").Value.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RecoveryRequests WHERE ConsumedAtUtc IS NOT NULL AND ResponseId = @ResponseId;", new { envelope.Payload.ResponseId }).ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces;").ShouldBe(1);
        fixture.Executor.QueryScalar<string>("SELECT InvalidationReason FROM Sessions WHERE SessionId = 'session-1';").ShouldBe("owner-recovery");
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'recovery.import' AND Result = 'success';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'session.invalidated' AND Result = 'success';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM HostOutbox WHERE MessageType = 'owner-password.reset';").ShouldBe(1);
    }

    [Fact]
    public void Cancel_consumes_nothing_and_preserves_credentials_and_sessions()
    {
        using var fixture = RecoveryImportFixture.Create();
        var ownerId = fixture.InsertOwnerWithCredential("old password");
        fixture.InsertActiveSession(ownerId, "session-1");
        var coordinator = fixture.CreateCoordinator();
        var challenge = coordinator.Verify(fixture.CreateStoredResponse()).Value;

        var result = coordinator.Cancel(challenge, CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
        fixture.CredentialStore.VerifyPassword(ownerId, "old password").Value.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RecoveryRequests WHERE ConsumedAtUtc IS NOT NULL;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE InvalidatedAtUtc IS NOT NULL;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM HostOutbox;").ShouldBe(0);
    }

    public static TheoryData<string> BindingMismatchFields => new()
    {
        "request-id",
        "nonce",
        "customer",
        "deployment",
        "device",
        "public-key",
        "vendor-key"
    };

    [Theory]
    [MemberData(nameof(BindingMismatchFields))]
    public void Verify_rejects_every_exact_request_binding_mismatch(string field)
    {
        using var fixture = RecoveryImportFixture.Create();
        fixture.InsertOwnerWithCredential("old password");
        var valid = fixture.CreateStoredResponse();
        var changed = field switch
        {
            "request-id" => valid.Payload with { RequestId = "018e51be-7f65-7bcd-8000-000000000099" },
            "nonce" => valid.Payload with { Nonce = ArtifactSignatureCodec.ToUnpaddedBase64Url(new byte[32]) },
            "customer" => valid.Payload with { CustomerId = "customer-other" },
            "deployment" => valid.Payload with { DeploymentId = "deployment-other" },
            "device" => valid.Payload with { DeviceId = "device:other" },
            "public-key" => valid.Payload with { RecoveryPublicKeyHash = new string('b', 64) },
            "vendor-key" => valid.Payload with { KeyId = "recovery-test-2026-02" },
            _ => throw new InvalidOperationException(field)
        };

        var result = fixture.CreateCoordinator().Verify(fixture.Sign(changed));

        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RecoveryRequests WHERE ConsumedAtUtc IS NOT NULL;").ShouldBe(0);
    }

    [Fact]
    public void Expired_wrong_purpose_wrong_signature_and_replay_fail_closed()
    {
        using var fixture = RecoveryImportFixture.Create();
        var ownerId = fixture.InsertOwnerWithCredential("old password");
        var valid = fixture.CreateStoredResponse();
        var coordinator = fixture.CreateCoordinator();

        coordinator.Verify(fixture.Sign(valid.Payload with { Purpose = ArtifactSignaturePurposes.PluginPackage })).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        var corrupted = valid with
        {
            Signature = valid.Signature with { Value = new string('A', 86) }
        };
        coordinator.Verify(corrupted).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        coordinator.Verify(fixture.Sign(valid.Payload with
        {
            IssuedAt = "2026-08-20T10:00:00.0000000Z",
            ExpiresAt = "2026-08-21T10:00:00.0000000Z"
        })).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        var challenge = coordinator.Verify(valid).Value;
        coordinator.ConfirmPasswordReset(challenge, "new owner password", CorrelationId.New()).IsSuccess.ShouldBeTrue();
        coordinator.Verify(valid).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        fixture.CredentialStore.VerifyPassword(ownerId, "new owner password").Value.ShouldBeTrue();
    }

    public static TheoryData<string> AtomicFailurePoints => new()
    {
        "credential-write",
        "request-consume",
        "nonce-consume",
        "session-invalidate",
        "recovery-audit",
        "session-audit",
        "outbox",
        "commit"
    };

    [Theory]
    [MemberData(nameof(AtomicFailurePoints))]
    public void Every_transaction_write_and_commit_failure_rolls_back_authoritative_state(string failurePoint)
    {
        using var fixture = RecoveryImportFixture.Create();
        var ownerId = fixture.InsertOwnerWithCredential("old password");
        fixture.InsertActiveSession(ownerId, "session-1");
        var valid = fixture.CreateStoredResponse();
        var coordinator = fixture.CreateCoordinator(new NamedFailureInjector(failurePoint));
        var challenge = coordinator.Verify(valid).Value;

        var result = coordinator.ConfirmPasswordReset(
            challenge,
            "new owner password",
            CorrelationId.New());

        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        fixture.CredentialStore.VerifyPassword(ownerId, "old password").Value.ShouldBeTrue();
        fixture.CredentialStore.VerifyPassword(ownerId, "new owner password").Value.ShouldBeFalse();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RecoveryRequests WHERE ConsumedAtUtc IS NOT NULL;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM ConsumedRecoveryNonces;").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE SessionId = 'session-1' AND InvalidatedAtUtc IS NULL;").ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE Result = 'success';").ShouldBe(0);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM HostOutbox;").ShouldBe(0);
    }

    private sealed class RecoveryImportFixture : IDisposable
    {
        private const string VendorKeyId = "recovery-test-2026-01";
        private const string PublicKeyHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private readonly ECDsa vendorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly RecordingProtector protector = new();

        private RecoveryImportFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(Factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            Time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T10:00:00Z"));
            CredentialStore = new CredentialStore(
                Executor,
                protector,
                new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }));
            Authority = new RecoveryAuthorityContext("customer-1", "deployment-1", "device:stable-1");
        }

        public string Root { get; }
        public EncryptedSqliteConnectionFactory Factory { get; }
        public HostPersistenceCommandExecutor Executor { get; }
        public FakeTimeProvider Time { get; }
        public CredentialStore CredentialStore { get; }
        public RecoveryAuthorityContext Authority { get; }

        public string InsertOwnerWithCredential(string password)
        {
            var ownerId = Guid.CreateVersion7().ToString("D");
            var now = Time.GetUtcNow().ToString("O");
            Executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, 'owner', NULL, 1, 1, @Now, @Now);",
                new { UserId = ownerId, Now = now });
            CredentialStore.CreateCredential(ownerId, password, Time.GetUtcNow()).IsSuccess.ShouldBeTrue();
            return ownerId;
        }

        public void InsertActiveSession(string ownerId, string sessionId)
        {
            var now = Time.GetUtcNow();
            Executor.Execute(
                "INSERT INTO Sessions(SessionId, UserId, CreatedAtUtc, LastActivityAtUtc, ExpiresAtUtc, InvalidatedAtUtc, InvalidationReason, SessionVersion) VALUES (@SessionId, @UserId, @Now, @Now, @ExpiresAtUtc, NULL, NULL, 1);",
                new { SessionId = sessionId, UserId = ownerId, Now = now.ToString("O"), ExpiresAtUtc = now.AddHours(8).ToString("O") });
        }

        public RecoveryResponseEnvelope CreateStoredResponse()
        {
            var payload = new RecoveryResponsePayload(
                2,
                ArtifactSignaturePurposes.OwnerRecoveryResponse,
                VendorKeyId,
                "018e51be-7f65-7bcd-8000-000000000001",
                "018e51be-7f65-7bcd-8000-000000000002",
                ArtifactSignatureCodec.ToUnpaddedBase64Url(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
                Authority.CustomerId,
                Authority.DeploymentId,
                Authority.DeviceId,
                PublicKeyHash,
                "reset-owner-password",
                "2026-08-21T09:30:00.0000000Z",
                "2026-08-22T09:30:00.0000000Z");
            InsertRequest(payload);
            return Sign(payload);
        }

        public RecoveryResponseEnvelope Sign(RecoveryResponsePayload payload)
        {
            var signature = vendorKey.SignData(
                RecoveryCanonicalJsonWriter.WritePayload(payload),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return new RecoveryResponseEnvelope(
                payload,
                new ArtifactSignatureEnvelope(
                    ArtifactSignaturePurposes.OwnerRecoveryResponse,
                    ArtifactSignatureProfile.Algorithm,
                    ArtifactSignatureProfile.Format,
                    payload.KeyId,
                    ArtifactSignatureCodec.ToUnpaddedBase64Url(signature)));
        }

        public RecoveryResponseImportCoordinator CreateCoordinator(IHostPersistenceTransactionFaultInjector? injector = null)
        {
            var audit = new DurableAuditTrail(Executor);
            var unitOfWork = new HostMutationUnitOfWork(
                new HostPersistenceTransactionScope(Factory, injector),
                audit,
                new HostTransactionOutbox(Time));
            return new RecoveryResponseImportCoordinator(
                Executor,
                new RecoveryResponseImporter(new EcdsaVendorPublicKey(vendorKey)),
                CredentialStore,
                unitOfWork,
                audit,
                Time,
                Authority,
                PublicKeyHash);
        }

        private void InsertRequest(RecoveryResponsePayload response)
        {
            var requestPayload = new RecoveryRequestPayload(
                2,
                ArtifactSignaturePurposes.OwnerRecoveryRequest,
                response.RequestId,
                response.Nonce,
                response.CustomerId,
                response.DeploymentId,
                response.DeviceId,
                response.RecoveryPublicKeyHash,
                "2026-08-21T09:00:00.0000000Z",
                "2026-08-21T10:00:00.0000000Z",
                response.KeyId);
            Executor.Execute(
                """
                INSERT INTO RecoveryRequests(
                    RequestId, NonceHash, CustomerId, DeploymentId, DeviceId,
                    RecoveryPublicKeyHash, VendorKeyId, PayloadHash,
                    IssuedAtUtc, ExpiresAtUtc, CreatedAtUtc, ConsumedAtUtc, ResponseId)
                VALUES(
                    @RequestId, @NonceHash, @CustomerId, @DeploymentId, @DeviceId,
                    @RecoveryPublicKeyHash, @VendorKeyId, @PayloadHash,
                    @IssuedAtUtc, @ExpiresAtUtc, @CreatedAtUtc, NULL, NULL);
                """,
                new
                {
                    requestPayload.RequestId,
                    NonceHash = RecoveryResponseImportCoordinator.HashNonce(requestPayload.Nonce),
                    requestPayload.CustomerId,
                    requestPayload.DeploymentId,
                    requestPayload.DeviceId,
                    requestPayload.RecoveryPublicKeyHash,
                    requestPayload.VendorKeyId,
                    PayloadHash = Convert.ToHexString(SHA256.HashData(RecoveryCanonicalJsonWriter.WriteRequestPayload(requestPayload))).ToLowerInvariant(),
                    IssuedAtUtc = requestPayload.IssuedAt,
                    ExpiresAtUtc = requestPayload.ExpiresAt,
                    CreatedAtUtc = Time.GetUtcNow().ToString("O")
                });
        }

        public static RecoveryImportFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-recovery-v2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new RecoveryImportFixture(root);
        }

        public void Dispose()
        {
            vendorKey.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class EcdsaVendorPublicKey(ECDsa key) : IVendorRecoveryPublicKey
    {
        public bool Verify(
            string keyId,
            string purpose,
            byte[] canonicalPayload,
            byte[] signature,
            DateTimeOffset now)
        {
            return keyId == "recovery-test-2026-01" &&
                purpose == ArtifactSignaturePurposes.OwnerRecoveryResponse &&
                key.VerifyData(
                    canonicalPayload,
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
    }

    private sealed class NamedFailureInjector(string failurePoint) : IHostPersistenceTransactionFaultInjector
    {
        private int auditNumber;

        public void BeforeCommand(int commandNumber, HostPersistenceCommandKind kind, string commandText)
        {
            var currentAuditNumber = commandText.Contains("INSERT INTO AuditLog", StringComparison.Ordinal)
                ? Interlocked.Increment(ref auditNumber)
                : 0;
            if ((failurePoint == "credential-write" && commandText.Contains("UPDATE Credentials", StringComparison.Ordinal)) ||
                (failurePoint == "request-consume" && commandText.Contains("UPDATE RecoveryRequests", StringComparison.Ordinal)) ||
                (failurePoint == "nonce-consume" && commandText.Contains("INSERT INTO ConsumedRecoveryNonces", StringComparison.Ordinal)) ||
                (failurePoint == "session-invalidate" && commandText.Contains("UPDATE Sessions", StringComparison.Ordinal)) ||
                (failurePoint == "recovery-audit" && currentAuditNumber == 1) ||
                (failurePoint == "session-audit" && currentAuditNumber == 2) ||
                (failurePoint == "outbox" && commandText.Contains("INSERT INTO HostOutbox", StringComparison.Ordinal)))
            {
                throw new SyntheticRecoveryFailure(failurePoint);
            }
        }

        public void BeforeCommit()
        {
            if (failurePoint == "commit")
            {
                throw new SyntheticRecoveryFailure(failurePoint);
            }
        }
    }

    private sealed class SyntheticRecoveryFailure(string point) : Exception(point);

    private sealed class RecordingProtector : IProtectedDataProtector
    {
        private static readonly byte[] Prefix = "protected:"u8.ToArray();

        public byte[] Protect(byte[] plaintext, byte[] entropy)
        {
            return Prefix.Concat(plaintext.Select(value => (byte)(value ^ 0x5A))).ToArray();
        }

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy)
        {
            return protectedBlob.Skip(Prefix.Length).Select(value => (byte)(value ^ 0x5A)).ToArray();
        }
    }
}

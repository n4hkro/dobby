using System.Security.Cryptography;
using Dobby.Core;
using Dobby.Security.Authentication;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class RecoveryResponseImporterUnitTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-21T10:00:00Z");
    private const string VendorKeyId = "recovery-test-2026-01";

    [Fact]
    public void Verify_accepts_strict_v2_purpose_bound_p1363_envelope()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var importer = new RecoveryResponseImporter(new EcdsaVendorPublicKey(key));
        var envelope = Envelope(key, Payload());

        var result = importer.Verify(envelope, Now);

        result.IsSuccess.ShouldBeTrue(result.ErrorCode?.ToString());
        result.Value.Payload.Action.ShouldBe("reset-owner-password");
    }

    [Fact]
    public void Verify_rejects_wrong_key_signature_format_purpose_and_key_id()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var importer = new RecoveryResponseImporter(new EcdsaVendorPublicKey(key));
        var payload = Payload();
        var canonical = RecoveryCanonicalJsonWriter.WritePayload(payload);
        var otherSignature = other.SignData(
            canonical,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var derSignature = key.SignData(
            canonical,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        importer.Verify(SignatureEnvelope(payload, otherSignature), Now).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        importer.Verify(SignatureEnvelope(payload, derSignature), Now).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        var wrongPurpose = Envelope(key, payload);
        wrongPurpose = wrongPurpose with
        {
            Signature = wrongPurpose.Signature with { Purpose = ArtifactSignaturePurposes.PluginPackage }
        };
        importer.Verify(wrongPurpose, Now).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        importer.Verify(
            Envelope(key, payload with { KeyId = "recovery-test-2026-02" }),
            Now).ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void Verify_rejects_expired_future_and_overlong_validity_windows()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var importer = new RecoveryResponseImporter(new EcdsaVendorPublicKey(key));

        importer.Verify(
            Envelope(key, Payload(issuedAt: "2026-08-20T09:00:00.0000000Z", expiresAt: "2026-08-21T10:00:00.0000000Z")),
            Now).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        importer.Verify(
            Envelope(key, Payload(issuedAt: "2026-08-21T10:06:00.0000000Z", expiresAt: "2026-08-22T10:00:00.0000000Z")),
            Now).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        importer.Verify(
            Envelope(key, Payload(issuedAt: "2026-08-21T09:00:00.0000000Z", expiresAt: "2026-08-22T09:00:01.0000000Z")),
            Now).ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Payload\":{},\"Signature\":{}}")]
    [InlineData("{\"payload\":{},\"payload\":{},\"signature\":{}}")]
    [InlineData("{\"payload\":{\"targetUserId\":\"user-2\"},\"signature\":{}}")]
    [InlineData("{\"payload\":{},\"signature\":{},\"legacy\":1}")]
    public void Read_envelope_rejects_missing_noncanonical_duplicate_and_unknown_members(string json)
    {
        Should.Throw<InvalidOperationException>(() => RecoveryResponseImporter.ReadEnvelope(json));
    }

    [Fact]
    public void Read_envelope_round_trips_only_lower_camel_signature_members()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var json = System.Text.Json.JsonSerializer.Serialize(Envelope(key, Payload()));

        json.ShouldContain("\"signature\"");
        json.ShouldContain("\"keyId\"");
        json.ShouldNotContain("\"KeyId\"", Case.Sensitive);
        RecoveryResponseImporter.ReadEnvelope(json).Payload.SchemaVersion.ShouldBe(2);
    }

    private static RecoveryResponsePayload Payload(
        string issuedAt = "2026-08-21T09:00:00.0000000Z",
        string expiresAt = "2026-08-22T09:00:00.0000000Z")
    {
        return new RecoveryResponsePayload(
            2,
            ArtifactSignaturePurposes.OwnerRecoveryResponse,
            VendorKeyId,
            "018e51be-7f65-7bcd-8000-000000000001",
            "018e51be-7f65-7bcd-8000-000000000002",
            ArtifactSignatureCodec.ToUnpaddedBase64Url(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
            "customer-1",
            "deployment-1",
            "device:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            new string('a', 64),
            "reset-owner-password",
            issuedAt,
            expiresAt);
    }

    private static RecoveryResponseEnvelope Envelope(ECDsa key, RecoveryResponsePayload payload)
    {
        var signature = key.SignData(
            RecoveryCanonicalJsonWriter.WritePayload(payload),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return SignatureEnvelope(payload, signature);
    }

    private static RecoveryResponseEnvelope SignatureEnvelope(
        RecoveryResponsePayload payload,
        byte[] signature)
    {
        return new RecoveryResponseEnvelope(
            payload,
            new ArtifactSignatureEnvelope(
                ArtifactSignaturePurposes.OwnerRecoveryResponse,
                ArtifactSignatureProfile.Algorithm,
                ArtifactSignatureProfile.Format,
                payload.KeyId,
                ArtifactSignatureCodec.ToUnpaddedBase64Url(signature)));
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
            return keyId == VendorKeyId &&
                purpose == ArtifactSignaturePurposes.OwnerRecoveryResponse &&
                key.VerifyData(
                    canonicalPayload,
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
    }
}

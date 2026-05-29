using System.Security.Cryptography;
using Dobby.Core;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class RecoveryResponseImporterUnitTests
{
    [Fact]
    public void Verify_accepts_standard_base64_padded_ieee_signature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = Payload(expiresAt: "2026-05-19T00:00:00Z");
        var signature = key.SignData(RecoveryCanonicalJsonWriter.WritePayload(payload), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var importer = new RecoveryResponseImporter(new EcdsaVendorPublicKey(key), new StaticDeviceFingerprint(Device(payload.DeviceIdentity)));

        var result = importer.Verify(new RecoveryResponseEnvelope(payload, Convert.ToBase64String(signature)), DateTimeOffset.Parse("2026-05-18T00:00:00Z"));

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.ErrorCode.ToString());
        result.Value.Payload.RequestId.ShouldBe("request-1");
    }

    [Fact]
    public void Verify_rejects_signature_fail_der_signature_base64url_no_padding_expired_and_device_mismatch()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = Payload(expiresAt: "2026-05-19T00:00:00Z");
        var importer = new RecoveryResponseImporter(new EcdsaVendorPublicKey(key), new StaticDeviceFingerprint(Device(payload.DeviceIdentity)));
        var canonical = RecoveryCanonicalJsonWriter.WritePayload(payload);
        var otherSignature = other.SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var derSignature = key.SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        var goodSignature = key.SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var base64Url = Convert.ToBase64String(goodSignature).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiredPayload = Payload(expiresAt: "2026-05-17T00:00:00Z");
        var expiredSignature = key.SignData(RecoveryCanonicalJsonWriter.WritePayload(expiredPayload), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var mismatchImporter = new RecoveryResponseImporter(new EcdsaVendorPublicKey(key), new StaticDeviceFingerprint(new DeviceFingerprint("x", "y", "z", "d", "e")));

        importer.Verify(new RecoveryResponseEnvelope(payload, Convert.ToBase64String(otherSignature)), DateTimeOffset.Parse("2026-05-18T00:00:00Z")).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        importer.Verify(new RecoveryResponseEnvelope(payload, Convert.ToBase64String(derSignature)), DateTimeOffset.Parse("2026-05-18T00:00:00Z")).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        importer.Verify(new RecoveryResponseEnvelope(payload, base64Url), DateTimeOffset.Parse("2026-05-18T00:00:00Z")).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        importer.Verify(new RecoveryResponseEnvelope(expiredPayload, Convert.ToBase64String(expiredSignature)), DateTimeOffset.Parse("2026-05-18T00:00:00Z")).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        mismatchImporter.Verify(new RecoveryResponseEnvelope(payload, Convert.ToBase64String(goodSignature)), DateTimeOffset.Parse("2026-05-18T00:00:00Z")).ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void Verify_rejects_unsupported_payload_type_and_key_version_even_when_signature_matches()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var wrongType = Payload(expiresAt: "2026-05-19T00:00:00Z", type: "dobby.owner-recovery-response.v2");
        var wrongKeyVersion = Payload(expiresAt: "2026-05-19T00:00:00Z", keyVersion: 2);
        var importer = new RecoveryResponseImporter(new EcdsaVendorPublicKey(key), new StaticDeviceFingerprint(Device(wrongType.DeviceIdentity)));

        importer.Verify(Envelope(key, wrongType), DateTimeOffset.Parse("2026-05-18T00:00:00Z")).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        importer.Verify(Envelope(key, wrongKeyVersion), DateTimeOffset.Parse("2026-05-18T00:00:00Z")).ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void Verify_rejects_signed_response_with_validity_window_longer_than_24_hours()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = Payload(expiresAt: "2026-05-20T00:00:01Z");
        var importer = new RecoveryResponseImporter(new EcdsaVendorPublicKey(key), new StaticDeviceFingerprint(Device(payload.DeviceIdentity)));

        var result = importer.Verify(Envelope(key, payload), DateTimeOffset.Parse("2026-05-18T00:00:00Z"));

        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void Read_envelope_rejects_missing_required_payload_and_signature_members()
    {
        Should.Throw<InvalidOperationException>(() => RecoveryResponseImporter.ReadEnvelope("{}"));
        Should.Throw<InvalidOperationException>(() => RecoveryResponseImporter.ReadEnvelope("""{"payload":null,"signature":"abc="}"""));
        Should.Throw<InvalidOperationException>(() => RecoveryResponseImporter.ReadEnvelope("""{"payload":{"type":"dobby.owner-recovery-response.v1"},"signature":null}"""));
    }

    [Fact]
    public void Read_envelope_rejects_non_canonical_property_casing()
    {
        Should.Throw<InvalidOperationException>(() => RecoveryResponseImporter.ReadEnvelope("""
            {"Payload":{"Type":"dobby.owner-recovery-response.v1","RequestId":"request-1","DeviceIdentity":{"MachineGuid":"a","CpuId":"b","MotherboardSerial":"c","DiskSerial":"d","MacAddress":"e"},"IssuedAt":"2026-05-18T00:00:00Z","ExpiresAt":"2026-05-19T00:00:00Z","Nonce":"nonce-1","KeyVersion":1},"Signature":"abc="}
            """));
    }

    [Fact]
    public void Read_envelope_rejects_managed_user_target_fields()
    {
        Should.Throw<InvalidOperationException>(() => RecoveryResponseImporter.ReadEnvelope("""
            {"payload":{"type":"dobby.owner-recovery-response.v1","requestId":"request-1","deviceIdentity":{"machineGuid":"a","cpuId":"b","motherboardSerial":"c","diskSerial":"d","macAddress":"e"},"issuedAt":"2026-05-18T00:00:00Z","expiresAt":"2026-05-19T00:00:00Z","nonce":"nonce-1","keyVersion":1,"targetUserId":"user-2"},"signature":"abc="}
            """));
    }

    private static RecoveryResponsePayload Payload(
        string expiresAt,
        string type = "dobby.owner-recovery-response.v1",
        int keyVersion = 1)
    {
        return new RecoveryResponsePayload(
            type,
            "request-1",
            new RecoveryDeviceIdentity("a", "b", "c", "d", "e"),
            "2026-05-18T00:00:00Z",
            expiresAt,
            "nonce-1",
            keyVersion);
    }

    private static RecoveryResponseEnvelope Envelope(ECDsa key, RecoveryResponsePayload payload)
    {
        var signature = key.SignData(RecoveryCanonicalJsonWriter.WritePayload(payload), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new RecoveryResponseEnvelope(payload, Convert.ToBase64String(signature));
    }

    private static DeviceFingerprint Device(RecoveryDeviceIdentity identity)
    {
        return new DeviceFingerprint(identity.MachineGuid, identity.CpuId, identity.MotherboardSerial, identity.DiskSerial, identity.MacAddress);
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
}

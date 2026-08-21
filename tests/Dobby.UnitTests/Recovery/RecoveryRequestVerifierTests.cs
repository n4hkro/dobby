using System.Security.Cryptography;
using Dobby.Core;
using Dobby.Security.Authentication;
using Dobby.Security.Trust;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class RecoveryRequestVerifierTests
{
    [Fact]
    public void Verify_requires_matching_provisioned_key_payload_and_possession_proof()
    {
        using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T10:00:00Z"));
        var exporter = new RecoveryRequestExporter(
            new DeviceRecoveryKeySigner(
                deviceKey.ExportPkcs8PrivateKey(),
                deviceKey.ExportSubjectPublicKeyInfo()),
            new RecoveryAuthorityContext("customer-1", "deployment-1", "device:stable-1"),
            time,
            VendorRecoveryPublicKey.DevQaKeyId);
        var envelope = exporter.CreateRequest().Value;
        var verifier = new RecoveryRequestVerifier();

        verifier.Verify(envelope, deviceKey.ExportSubjectPublicKeyInfo(), time.GetUtcNow()).IsSuccess.ShouldBeTrue();
        verifier.Verify(envelope, wrongKey.ExportSubjectPublicKeyInfo(), time.GetUtcNow()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        verifier.Verify(
            envelope with { Payload = envelope.Payload with { CustomerId = "customer-other" } },
            deviceKey.ExportSubjectPublicKeyInfo(),
            time.GetUtcNow()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        verifier.Verify(
            envelope with
            {
                PossessionProof = envelope.PossessionProof with { Purpose = ArtifactSignaturePurposes.PluginPackage }
            },
            deviceKey.ExportSubjectPublicKeyInfo(),
            time.GetUtcNow()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void Verify_rejects_expired_request_and_reader_rejects_duplicate_unknown_or_legacy_members()
    {
        using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-21T10:00:00Z"));
        var envelope = new RecoveryRequestExporter(
            new DeviceRecoveryKeySigner(
                deviceKey.ExportPkcs8PrivateKey(),
                deviceKey.ExportSubjectPublicKeyInfo()),
            new RecoveryAuthorityContext("customer-1", "deployment-1", "device:stable-1"),
            time,
            VendorRecoveryPublicKey.DevQaKeyId,
            TimeSpan.FromMinutes(1)).CreateRequest().Value;
        time.Advance(TimeSpan.FromMinutes(1));

        new RecoveryRequestVerifier().Verify(
            envelope,
            deviceKey.ExportSubjectPublicKeyInfo(),
            time.GetUtcNow()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        Should.Throw<InvalidOperationException>(() => RecoveryRequestVerifier.ReadEnvelope(
            "{\"payload\":{},\"payload\":{},\"possessionProof\":{}}"));
        Should.Throw<InvalidOperationException>(() => RecoveryRequestVerifier.ReadEnvelope(
            "{\"type\":\"dobby.owner-recovery-request.v1\"}"));
        Should.Throw<InvalidOperationException>(() => RecoveryRequestVerifier.ReadEnvelope(
            "{\"payload\":{},\"possessionProof\":{},\"targetUserId\":\"user-1\"}"));
    }
}

using System.Security.Cryptography;
using Dobby.Security.Trust;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class PluginTrustVerifierTests
{
    [Fact]
    public void Verify_accepts_signature_from_selected_profile()
    {
        using var key = PluginTrustTestData.CreateKeyPair(out _, out var publicKeyPem);
        var verifier = new PluginTrustVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = publicKeyPem
            }));

        var signature = key.SignData(
            PluginTrustTestData.CanonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var payload = new PluginTrustPayload(
            "dobby.vendor",
            ArtifactSignaturePurposes.PluginPackage,
            ArtifactSignatureProfile.Algorithm,
            ArtifactSignatureProfile.Format,
            PluginTrustPublicKeyProvider.ProductionPluginPackageKeyId,
            PluginTrustTestData.CanonicalPayload,
            ArtifactSignatureCodec.ToUnpaddedBase64Url(signature));

        var result = verifier.Verify(payload);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Verify_rejects_devqa_vendor_in_production()
    {
        using var key = PluginTrustTestData.CreateKeyPair(out _, out var publicKeyPem);
        var verifier = new PluginTrustVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = publicKeyPem
            }));

        var signature = key.SignData(
            PluginTrustTestData.CanonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var payload = new PluginTrustPayload(
            "dobby.dev-qa",
            ArtifactSignaturePurposes.PluginPackage,
            ArtifactSignatureProfile.Algorithm,
            ArtifactSignatureProfile.Format,
            PluginTrustPublicKeyProvider.ProductionPluginPackageKeyId,
            PluginTrustTestData.CanonicalPayload,
            ArtifactSignatureCodec.ToUnpaddedBase64Url(signature));

        var result = verifier.Verify(payload);

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("trust-failed");
    }

    [Theory]
    [InlineData("dobby.plugin-license", "ECDSA-P256-SHA256", "IeeeP1363FixedFieldConcatenation", "plugin-prod-2026-01", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("dobby.plugin-package", "RSA-SHA256", "IeeeP1363FixedFieldConcatenation", "plugin-prod-2026-01", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("dobby.plugin-package", "ECDSA-P256-SHA256", "DER", "plugin-prod-2026-01", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("dobby.plugin-package", "ECDSA-P256-SHA256", "IeeeP1363FixedFieldConcatenation", "", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("dobby.plugin-package", "ECDSA-P256-SHA256", "IeeeP1363FixedFieldConcatenation", "plugin-prod-2026-01", "not-valid@")]
    public void Verify_rejects_malformed_signature_contract_values(
        string purpose,
        string algorithm,
        string format,
        string keyId,
        string signatureValue)
    {
        using var key = PluginTrustTestData.CreateKeyPair(out _, out var publicKeyPem);
        var verifier = new PluginTrustVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = publicKeyPem
            }));
        var payload = new PluginTrustPayload(
            "dobby.vendor",
            purpose,
            algorithm,
            format,
            keyId,
            PluginTrustTestData.CanonicalPayload,
            signatureValue);

        var result = verifier.Verify(payload);

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("signature-failed");
    }

    [Fact]
    public void Verify_rejects_empty_canonical_payload_before_key_resolution()
    {
        var verifier = new PluginTrustVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>()));
        var payload = new PluginTrustPayload(
            "dobby.vendor",
            ArtifactSignaturePurposes.PluginPackage,
            ArtifactSignatureProfile.Algorithm,
            ArtifactSignatureProfile.Format,
            PluginTrustPublicKeyProvider.ProductionPluginPackageKeyId,
            [],
            "AQ");

        var result = verifier.Verify(payload);

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("signature-failed");
        result.Message.ShouldContain("malformed");
    }

    [Fact]
    public void Verify_rejects_DER_signature_even_when_the_key_and_payload_are_valid()
    {
        using var key = PluginTrustTestData.CreateKeyPair(out _, out var publicKeyPem);
        var verifier = new PluginTrustVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = publicKeyPem
            }));
        var der = key.SignData(
            PluginTrustTestData.CanonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        var result = verifier.Verify(new PluginTrustPayload(
            "dobby.vendor",
            ArtifactSignaturePurposes.PluginPackage,
            ArtifactSignatureProfile.Algorithm,
            ArtifactSignatureProfile.Format,
            PluginTrustPublicKeyProvider.ProductionPluginPackageKeyId,
            PluginTrustTestData.CanonicalPayload,
            ArtifactSignatureCodec.ToUnpaddedBase64Url(der)));

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("signature-failed");
    }

    [Fact]
    public void Verify_accepts_rotation_overlap_then_rejects_the_retired_key_deterministically()
    {
        using var oldKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var activeKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var ring = new ArtifactPublicKeyRing(
            [
                new ArtifactVerificationKey("plugin-prod-old", ArtifactSignaturePurposes.PluginPackage, oldKey.ExportSubjectPublicKeyInfo(), now.AddYears(-1), now.AddMonths(1), false),
                new ArtifactVerificationKey("plugin-prod-active", ArtifactSignaturePurposes.PluginPackage, activeKey.ExportSubjectPublicKeyInfo(), now.AddMonths(-1), null, false)
            ],
            productionProfile: true);
        var verifier = new PluginTrustVerifier(ring, DobbyPluginTrustProfile.Production, clock);
        var signature = oldKey.SignData(
            PluginTrustTestData.CanonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var payload = new PluginTrustPayload(
            "dobby.vendor",
            ArtifactSignaturePurposes.PluginPackage,
            ArtifactSignatureProfile.Algorithm,
            ArtifactSignatureProfile.Format,
            "plugin-prod-old",
            PluginTrustTestData.CanonicalPayload,
            ArtifactSignatureCodec.ToUnpaddedBase64Url(signature));

        verifier.Verify(payload).IsSuccess.ShouldBeTrue();
        clock.SetUtcNow(now.AddMonths(1));
        verifier.Verify(payload).IsSuccess.ShouldBeFalse();
    }
}

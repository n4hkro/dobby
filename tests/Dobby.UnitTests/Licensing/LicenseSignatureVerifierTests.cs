using System.Security.Cryptography;
using Dobby.Licensing;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicenseSignatureVerifierTests
{
    [Fact]
    public void Verify_accepts_valid_p256_P1363_signature_from_license_trust_resource()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var canonicalPayload = Encoding.UTF8.GetBytes("license-domain-payload");
        var signature = LicenseArtifactFixtureBuilder.SignToBase64Url(key, canonicalPayload);

        var result = CreateVerifier(publicKeyPem).Verify(Signature(signature), canonicalPayload);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Verify_rejects_signature_mismatch_without_exposing_raw_details()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var signature = LicenseArtifactFixtureBuilder.SignToBase64Url(key, "first"u8.ToArray());

        var result = CreateVerifier(publicKeyPem).Verify(Signature(signature), "second"u8.ToArray());

        result.ReasonCode.ShouldBe(LicenseRejectReasons.InvalidSignature);
        result.SupportCode.ShouldBe("signature-failed");
    }

    [Fact]
    public void Verify_maps_missing_license_trust_resource_to_untrusted_vendor()
    {
        var verifier = new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>(),
            ArtifactSignaturePurposes.PluginLicense));

        var result = verifier.Verify(
            Signature(ArtifactSignatureCodec.ToUnpaddedBase64Url(new byte[64])),
            "payload"u8.ToArray());

        result.ReasonCode.ShouldBe(LicenseRejectReasons.UntrustedVendor);
        result.SupportCode.ShouldBe("trust-failed");
    }

    [Theory]
    [InlineData("dobby.plugin-package", "ECDSA-P256-SHA256", "IeeeP1363FixedFieldConcatenation", "license-prod-2026-01")]
    [InlineData("dobby.plugin-license", "RSA-SHA256", "IeeeP1363FixedFieldConcatenation", "license-prod-2026-01")]
    [InlineData("dobby.plugin-license", "ECDSA-P256-SHA256", "DER", "license-prod-2026-01")]
    [InlineData("dobby.plugin-license", "ECDSA-P256-SHA256", "IeeeP1363FixedFieldConcatenation", "")]
    public void Verify_rejects_cross_purpose_and_malformed_profile(
        string purpose,
        string algorithm,
        string format,
        string keyId)
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var signature = new LicenseSignatureBlock(
            purpose,
            algorithm,
            format,
            keyId,
            ArtifactSignatureCodec.ToUnpaddedBase64Url(new byte[64]));

        CreateVerifier(publicKeyPem).Verify(signature, "payload"u8.ToArray()).ReasonCode
            .ShouldBe(LicenseRejectReasons.InvalidSignature);
    }

    [Fact]
    public void Verify_rejects_DER_and_padded_base64url_encodings()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var payload = "payload"u8.ToArray();
        var der = key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        var verifier = CreateVerifier(publicKeyPem);

        verifier.Verify(Signature(ArtifactSignatureCodec.ToUnpaddedBase64Url(der)), payload).IsSuccess.ShouldBeFalse();
        verifier.Verify(Signature(LicenseArtifactFixtureBuilder.SignToBase64Url(key, payload) + "="), payload).IsSuccess.ShouldBeFalse();
    }

    private static LicenseSignatureBlock Signature(string value) => new(
        ArtifactSignaturePurposes.PluginLicense,
        ArtifactSignatureProfile.Algorithm,
        ArtifactSignatureProfile.Format,
        PluginTrustPublicKeyProvider.ProductionPluginLicenseKeyId,
        value);

    private static LicenseSignatureVerifier CreateVerifier(byte[] publicKeyPem)
    {
        return new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionLicenseResourceName] = publicKeyPem
            },
            ArtifactSignaturePurposes.PluginLicense));
    }
}

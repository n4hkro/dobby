using Dobby.Licensing;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicenseSignatureVerifierTests
{
    [Fact]
    public void Verify_accepts_valid_p256_der_signature_from_selected_trust_resource()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var canonicalPayload = Encoding.UTF8.GetBytes("{\"schemaVersion\":2}");
        var signature = LicenseArtifactFixtureBuilder.SignToBase64Url(key, canonicalPayload);
        var verifier = CreateVerifier(publicKeyPem);

        var result = verifier.Verify(
            new LicenseSignatureBlock("ECDSA-P256-SHA256", 1, signature),
            canonicalPayload);

        result.IsSuccess.ShouldBeTrue();
        result.ReasonCode.ShouldBeEmpty();
    }

    [Fact]
    public void Verify_rejects_signature_mismatch_without_exposing_raw_details()
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var signature = LicenseArtifactFixtureBuilder.SignToBase64Url(
            key,
            Encoding.UTF8.GetBytes("{\"schemaVersion\":2}"));
        var verifier = CreateVerifier(publicKeyPem);

        var result = verifier.Verify(
            new LicenseSignatureBlock("ECDSA-P256-SHA256", 1, signature),
            Encoding.UTF8.GetBytes("{\"schemaVersion\":3}"));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-signature");
        result.SupportCode.ShouldBe("signature-failed");
    }

    [Fact]
    public void Verify_maps_missing_trust_resource_to_untrusted_vendor()
    {
        var verifier = new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>()));

        var result = verifier.Verify(
            new LicenseSignatureBlock("ECDSA-P256-SHA256", 1, "AQID"),
            Encoding.UTF8.GetBytes("{\"schemaVersion\":2}"));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("untrusted-vendor");
        result.SupportCode.ShouldBe("trust-failed");
    }

    [Theory]
    [InlineData("RSA-SHA256", 1, "AQID")]
    [InlineData("ECDSA-P256-SHA256", 2, "AQID")]
    [InlineData("ECDSA-P256-SHA256", 1, "AQID==")]
    public void Verify_rejects_malformed_signature_contract_values(
        string algorithm,
        int keyVersion,
        string value)
    {
        using var key = LicenseArtifactFixtureBuilder.CreateKeyPair(out var publicKeyPem);
        var verifier = CreateVerifier(publicKeyPem);

        var result = verifier.Verify(
            new LicenseSignatureBlock(algorithm, keyVersion, value),
            Encoding.UTF8.GetBytes("{\"schemaVersion\":2}"));

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("invalid-signature");
    }

    private static LicenseSignatureVerifier CreateVerifier(byte[] publicKeyPem)
    {
        return new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>
            {
                [PluginTrustPublicKeyProvider.ProductionResourceName] = publicKeyPem
            }));
    }
}

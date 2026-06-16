using System.Security.Cryptography;
using Dobby.Security.Trust;
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
            DSASignatureFormat.Rfc3279DerSequence);
        var payload = new PluginTrustPayload(
            "dobby.vendor",
            1,
            "ECDSA-P256-SHA256",
            PluginTrustTestData.CanonicalPayload,
            PluginTrustTestData.ToBase64Url(signature));

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
            DSASignatureFormat.Rfc3279DerSequence);
        var payload = new PluginTrustPayload(
            "dobby.dev-qa",
            1,
            "ECDSA-P256-SHA256",
            PluginTrustTestData.CanonicalPayload,
            PluginTrustTestData.ToBase64Url(signature));

        var result = verifier.Verify(payload);

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("trust-failed");
    }

    [Theory]
    [InlineData("RSA-SHA256", 1, "AQ", "signature-failed")]
    [InlineData("ECDSA-P256-SHA256", 2, "AQ", "signature-failed")]
    [InlineData("ECDSA-P256-SHA256", 1, "not-valid@", "signature-failed")]
    public void Verify_rejects_malformed_signature_contract_values(
        string algorithm,
        int keyVersion,
        string signatureValue,
        string expectedCode)
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
            keyVersion,
            algorithm,
            PluginTrustTestData.CanonicalPayload,
            signatureValue);

        var result = verifier.Verify(payload);

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe(expectedCode);
    }

    [Fact]
    public void Verify_rejects_empty_canonical_payload_before_key_resolution()
    {
        var verifier = new PluginTrustVerifier(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.Production,
            new Dictionary<string, byte[]>()));
        var payload = new PluginTrustPayload(
            "dobby.vendor",
            1,
            "ECDSA-P256-SHA256",
            [],
            "AQ");

        var result = verifier.Verify(payload);

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("signature-failed");
        result.Message.ShouldContain("empty");
    }
}

using System.Security.Cryptography;
using System.Text;
using Dobby.Security.Authentication;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class VendorRecoveryPublicKeyTests
{
    [Fact]
    public void Production_vendor_recovery_public_key_rejects_malformed_signature_material()
    {
        IVendorRecoveryPublicKey publicKey = new VendorRecoveryPublicKey();

        publicKey.Verify([1, 2, 3], [4, 5, 6]).ShouldBeFalse();
    }

    [Fact]
    public void Vendor_recovery_public_key_verifies_p1363_signature_from_embedded_trust_resource()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = Encoding.UTF8.GetBytes("""{"requestId":"request-1"}""");
        var signature = key.SignData(
            payload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var publicKey = new VendorRecoveryPublicKey(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.DevQa,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [PluginTrustPublicKeyProvider.DevQaResourceName] = EncodePem(key.ExportSubjectPublicKeyInfo())
            }));

        publicKey.Verify(payload, signature).ShouldBeTrue();
        publicKey.Verify(Encoding.UTF8.GetBytes("""{"requestId":"other"}"""), signature).ShouldBeFalse();
    }

    [Fact]
    public void Vendor_recovery_public_key_fails_closed_when_expected_trust_resource_is_missing()
    {
        var publicKey = new VendorRecoveryPublicKey(new PluginTrustPublicKeyProvider(
            DobbyPluginTrustProfile.DevQa,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)));

        publicKey.Verify([1, 2, 3], new byte[64]).ShouldBeFalse();
    }

    private static byte[] EncodePem(byte[] der)
    {
        var base64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        return Encoding.UTF8.GetBytes($"-----BEGIN PUBLIC KEY-----\n{base64}\n-----END PUBLIC KEY-----\n");
    }
}

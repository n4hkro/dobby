using System.Security.Cryptography;
using Dobby.Security.Trust;

namespace Dobby.Security.Authentication;

public sealed class VendorRecoveryPublicKey : IVendorRecoveryPublicKey
{
    private readonly PluginTrustPublicKeyProvider publicKeyProvider;

    public VendorRecoveryPublicKey()
        : this(new PluginTrustPublicKeyProvider())
    {
    }

    internal VendorRecoveryPublicKey(PluginTrustPublicKeyProvider publicKeyProvider)
    {
        this.publicKeyProvider = publicKeyProvider ?? throw new ArgumentNullException(nameof(publicKeyProvider));
    }

    public bool Verify(byte[] canonicalPayload, byte[] signature)
    {
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        ArgumentNullException.ThrowIfNull(signature);

        if (canonicalPayload.Length == 0 || signature.Length != 64)
        {
            return false;
        }

        var publicKey = publicKeyProvider.Resolve();
        if (!publicKey.IsSuccess)
        {
            return false;
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKey.Value!.PublicKeyBytes, out _);
            return ecdsa.VerifyData(
                canonicalPayload,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}

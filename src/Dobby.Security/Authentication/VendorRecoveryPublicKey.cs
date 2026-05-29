namespace Dobby.Security.Authentication;

public sealed class VendorRecoveryPublicKey : IVendorRecoveryPublicKey
{
    public bool Verify(byte[] canonicalPayload, byte[] signature)
    {
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        ArgumentNullException.ThrowIfNull(signature);

        return false;
    }
}

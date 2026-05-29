namespace Dobby.Security.Authentication;

public interface IVendorRecoveryPublicKey
{
    bool Verify(byte[] canonicalPayload, byte[] signature);
}

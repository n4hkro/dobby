namespace Dobby.Security.Authentication;

public interface IVendorRecoveryPublicKey
{
    bool Verify(
        string keyId,
        string purpose,
        byte[] canonicalPayload,
        byte[] signature,
        DateTimeOffset now);
}

using System.Security.Cryptography;
using System.Text;

namespace Dobby.Security.Trust;

public interface IPluginTrustVerifier
{
    PluginTrustVerificationResult Verify(PluginTrustPayload payload);
}

public sealed class PluginTrustVerifier : IPluginTrustVerifier
{
    private const string DevQaVendorId = "dobby.dev-qa";

    private readonly PluginTrustPublicKeyProvider publicKeyProvider;

    public PluginTrustVerifier(DobbyPluginTrustProfile profile = DobbyPluginTrustProfile.Production)
        : this(new PluginTrustPublicKeyProvider(profile))
    {
    }

    public PluginTrustVerifier(PluginTrustPublicKeyProvider publicKeyProvider)
    {
        this.publicKeyProvider = publicKeyProvider ?? throw new ArgumentNullException(nameof(publicKeyProvider));
    }

    public PluginTrustVerificationResult Verify(PluginTrustPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!payload.Algorithm.Equals("ECDSA-P256-SHA256", StringComparison.Ordinal))
        {
            return PluginTrustVerificationResult.Failure("signature-failed", "Unsupported signature algorithm.");
        }

        if (payload.KeyVersion != 1)
        {
            return PluginTrustVerificationResult.Failure("signature-failed", "Unsupported signature key version.");
        }

        if (payload.CanonicalPayload.Length == 0)
        {
            return PluginTrustVerificationResult.Failure("signature-failed", "Canonical payload is empty.");
        }

        var keyResult = publicKeyProvider.Resolve();
        if (!keyResult.IsSuccess)
        {
            return PluginTrustVerificationResult.Failure(keyResult.Code, keyResult.Message);
        }

        var publicKey = keyResult.Value!;

        if (!IsVendorAllowed(payload.VendorId, publicKey.Profile))
        {
            return PluginTrustVerificationResult.Failure("trust-failed", $"Vendor '{payload.VendorId}' is not approved for profile '{publicKey.Profile}'.");
        }

        if (!TryDecodeSignature(payload.SignatureValue, out var signature))
        {
            return PluginTrustVerificationResult.Failure("signature-failed", "Signature value is malformed.");
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKey.PublicKeyBytes, out _);
            var verified = ecdsa.VerifyData(
                payload.CanonicalPayload,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);

            return verified
                ? PluginTrustVerificationResult.Success()
                : PluginTrustVerificationResult.Failure("signature-failed", "Signature verification failed.");
        }
        catch (CryptographicException exception)
        {
            return PluginTrustVerificationResult.Failure("trust-failed", exception.Message);
        }
    }

    private static bool IsVendorAllowed(string vendorId, DobbyPluginTrustProfile profile)
    {
        if (profile == DobbyPluginTrustProfile.DevQa)
        {
            return vendorId.Equals(DevQaVendorId, StringComparison.Ordinal);
        }

        return !vendorId.Equals(DevQaVendorId, StringComparison.Ordinal);
    }

    private static bool TryDecodeSignature(string signatureValue, out byte[] signature)
    {
        try
        {
            signature = Base64UrlDecode(signatureValue);
            return signature.Length > 0;
        }
        catch
        {
            signature = [];
            return false;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value
            .Replace('-', '+')
            .Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
            case 1:
                throw new FormatException("Invalid base64url value.");
        }

        return Convert.FromBase64String(padded);
    }
}

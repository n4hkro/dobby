using System.Security.Cryptography;
using Dobby.Security.Trust;

namespace Dobby.Licensing;

public sealed class LicenseSignatureVerifier
{
    private const string ExpectedAlgorithm = "ECDSA-P256-SHA256";
    private readonly PluginTrustPublicKeyProvider publicKeyProvider;

    public LicenseSignatureVerifier(DobbyPluginTrustProfile profile = DobbyPluginTrustProfile.Production)
        : this(new PluginTrustPublicKeyProvider(profile))
    {
    }

    public LicenseSignatureVerifier(PluginTrustPublicKeyProvider publicKeyProvider)
    {
        this.publicKeyProvider = publicKeyProvider ?? throw new ArgumentNullException(nameof(publicKeyProvider));
    }

    public LicenseSignatureVerificationResult Verify(LicenseSignatureBlock signature, byte[] canonicalPayload)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(canonicalPayload);

        if (!signature.Algorithm.Equals(ExpectedAlgorithm, StringComparison.Ordinal) ||
            signature.KeyVersion != 1 ||
            canonicalPayload.Length == 0 ||
            !TryDecodeSignature(signature.Value, out var signatureBytes))
        {
            return LicenseSignatureVerificationResult.Failure(
                LicenseRejectReasons.InvalidSignature,
                "signature-failed");
        }

        var publicKeyResult = publicKeyProvider.Resolve();
        if (!publicKeyResult.IsSuccess)
        {
            return LicenseSignatureVerificationResult.Failure(
                LicenseRejectReasons.UntrustedVendor,
                publicKeyResult.Code);
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKeyResult.Value!.PublicKeyBytes, out _);
            var verified = ecdsa.VerifyData(
                canonicalPayload,
                signatureBytes,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);

            return verified
                ? LicenseSignatureVerificationResult.Success()
                : LicenseSignatureVerificationResult.Failure(
                    LicenseRejectReasons.InvalidSignature,
                    "signature-failed");
        }
        catch (CryptographicException)
        {
            return LicenseSignatureVerificationResult.Failure(
                LicenseRejectReasons.UntrustedVendor,
                "trust-failed");
        }
    }

    private static bool TryDecodeSignature(string value, out byte[] signatureBytes)
    {
        signatureBytes = [];
        if (string.IsNullOrWhiteSpace(value) || value.Contains('=', StringComparison.Ordinal) || value.Length % 4 == 1)
        {
            return false;
        }

        foreach (var character in value)
        {
            var valid = character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-'
                or '_';
            if (!valid)
            {
                return false;
            }
        }

        try
        {
            signatureBytes = Base64UrlDecode(value);
            return signatureBytes.Length > 0;
        }
        catch (FormatException)
        {
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

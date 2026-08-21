using Dobby.Security.Trust;

namespace Dobby.Licensing;

public sealed class LicenseSignatureVerifier
{
    private readonly PluginTrustPublicKeyProvider? publicKeyProvider;
    private readonly ArtifactPublicKeyRing? injectedKeyRing;
    private readonly DobbyPluginTrustProfile profile;
    private readonly TimeProvider timeProvider;

    public LicenseSignatureVerifier(DobbyPluginTrustProfile profile = DobbyPluginTrustProfile.Production)
        : this(
            new PluginTrustPublicKeyProvider(profile, ArtifactSignaturePurposes.PluginLicense),
            TimeProvider.System)
    {
    }

    public LicenseSignatureVerifier(
        PluginTrustPublicKeyProvider publicKeyProvider,
        TimeProvider? timeProvider = null)
    {
        this.publicKeyProvider = publicKeyProvider ?? throw new ArgumentNullException(nameof(publicKeyProvider));
        profile = publicKeyProvider.Profile;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public LicenseSignatureVerifier(
        ArtifactPublicKeyRing keyRing,
        DobbyPluginTrustProfile profile,
        TimeProvider? timeProvider = null)
    {
        injectedKeyRing = keyRing ?? throw new ArgumentNullException(nameof(keyRing));
        this.profile = profile;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public LicenseSignatureVerificationResult Verify(
        LicenseSignatureBlock signature,
        byte[] canonicalPayload)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(canonicalPayload);

        if (!signature.Purpose.Equals(ArtifactSignaturePurposes.PluginLicense, StringComparison.Ordinal) ||
            !signature.Algorithm.Equals(ArtifactSignatureProfile.Algorithm, StringComparison.Ordinal) ||
            !signature.Format.Equals(ArtifactSignatureProfile.Format, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(signature.KeyId) ||
            canonicalPayload.Length == 0 ||
            !ArtifactSignatureCodec.TryDecodeUnpaddedBase64Url(
                signature.Value,
                ArtifactSignatureProfile.P256SignatureLength,
                out var signatureBytes))
        {
            return LicenseSignatureVerificationResult.Failure(
                LicenseRejectReasons.InvalidSignature,
                "signature-failed");
        }

        var ring = injectedKeyRing;
        if (ring is null)
        {
            var publicKeyResult = publicKeyProvider!.Resolve();
            if (!publicKeyResult.IsSuccess ||
                !publicKeyResult.Value!.Purpose.Equals(ArtifactSignaturePurposes.PluginLicense, StringComparison.Ordinal))
            {
                return LicenseSignatureVerificationResult.Failure(
                    LicenseRejectReasons.UntrustedVendor,
                    "trust-failed");
            }

            var key = publicKeyResult.Value;
            ring = new ArtifactPublicKeyRing(
                [new ArtifactVerificationKey(
                    key.KeyId,
                    key.Purpose,
                    key.PublicKeyBytes,
                    key.NotBeforeUtc,
                    key.RetiredAtUtc,
                    key.IsTestOnly)],
                productionProfile: profile == DobbyPluginTrustProfile.Production);
        }

        return ring.Verify(
                signature.KeyId,
                signature.Purpose,
                canonicalPayload,
                signatureBytes,
                timeProvider.GetUtcNow())
            ? LicenseSignatureVerificationResult.Success()
            : LicenseSignatureVerificationResult.Failure(
                LicenseRejectReasons.InvalidSignature,
                "signature-failed");
    }
}

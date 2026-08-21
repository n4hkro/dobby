namespace Dobby.Security.Trust;

public interface IPluginTrustVerifier
{
    PluginTrustVerificationResult Verify(PluginTrustPayload payload);
}

public sealed class PluginTrustVerifier : IPluginTrustVerifier
{
    private const string DevQaVendorId = "dobby.dev-qa";

    private readonly PluginTrustPublicKeyProvider? publicKeyProvider;
    private readonly ArtifactPublicKeyRing? injectedKeyRing;
    private readonly DobbyPluginTrustProfile profile;
    private readonly TimeProvider timeProvider;

    public PluginTrustVerifier(DobbyPluginTrustProfile profile = DobbyPluginTrustProfile.Production)
        : this(new PluginTrustPublicKeyProvider(profile), profile, TimeProvider.System)
    {
    }

    public PluginTrustVerifier(
        PluginTrustPublicKeyProvider publicKeyProvider,
        DobbyPluginTrustProfile? profile = null,
        TimeProvider? timeProvider = null)
    {
        this.publicKeyProvider = publicKeyProvider ?? throw new ArgumentNullException(nameof(publicKeyProvider));
        this.profile = profile ?? publicKeyProvider.Profile;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public PluginTrustVerifier(
        ArtifactPublicKeyRing keyRing,
        DobbyPluginTrustProfile profile,
        TimeProvider? timeProvider = null)
    {
        injectedKeyRing = keyRing ?? throw new ArgumentNullException(nameof(keyRing));
        this.profile = profile;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public PluginTrustVerificationResult Verify(PluginTrustPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!payload.Purpose.Equals(ArtifactSignaturePurposes.PluginPackage, StringComparison.Ordinal) ||
            !payload.Algorithm.Equals(ArtifactSignatureProfile.Algorithm, StringComparison.Ordinal) ||
            !payload.Format.Equals(ArtifactSignatureProfile.Format, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(payload.KeyId) ||
            payload.CanonicalPayload.Length == 0 ||
            !ArtifactSignatureCodec.TryDecodeUnpaddedBase64Url(
                payload.SignatureValue,
                ArtifactSignatureProfile.P256SignatureLength,
                out var signature))
        {
            return PluginTrustVerificationResult.Failure("signature-failed", "Plugin signature profile is malformed or unsupported.");
        }

        if (!IsVendorAllowed(payload.VendorId, profile))
        {
            return PluginTrustVerificationResult.Failure("trust-failed", $"Vendor '{payload.VendorId}' is not approved for profile '{profile}'.");
        }

        var ring = injectedKeyRing;
        if (ring is null)
        {
            var keyResult = publicKeyProvider!.Resolve();
            if (!keyResult.IsSuccess)
            {
                return PluginTrustVerificationResult.Failure(keyResult.Code, keyResult.Message);
            }

            var key = keyResult.Value!;
            ring = new ArtifactPublicKeyRing(
                [
                    new ArtifactVerificationKey(
                        key.KeyId,
                        key.Purpose,
                        key.PublicKeyBytes,
                        key.NotBeforeUtc,
                        key.RetiredAtUtc,
                        key.IsTestOnly)
                ],
                productionProfile: profile == DobbyPluginTrustProfile.Production);
        }

        return ring.Verify(
                payload.KeyId,
                payload.Purpose,
                payload.CanonicalPayload,
                signature,
                timeProvider.GetUtcNow())
            ? PluginTrustVerificationResult.Success()
            : PluginTrustVerificationResult.Failure("signature-failed", "Plugin signature key is unknown, inactive, retired, purpose-mismatched, or verification failed.");
    }

    private static bool IsVendorAllowed(string vendorId, DobbyPluginTrustProfile profile)
    {
        return profile == DobbyPluginTrustProfile.DevQa
            ? vendorId.Equals(DevQaVendorId, StringComparison.Ordinal)
            : !vendorId.Equals(DevQaVendorId, StringComparison.Ordinal);
    }
}

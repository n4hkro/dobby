using System.Security.Cryptography;
using Dobby.Security.Trust;

namespace Dobby.Security.Authentication;

public sealed class VendorRecoveryPublicKey : IVendorRecoveryPublicKey
{
    public const string ProductionKeyId = "recovery-prod-2026-01";
    public const string DevQaKeyId = "recovery-test-2026-01";
    public const string ProductionResourceName = "Dobby.Security.Trust.vendor-recovery-public-key.pem";
    public const string DevQaResourceName = "Dobby.Security.Trust.dev-qa-recovery-public-key.pem";

    private readonly ArtifactPublicKeyRing keyRing;

    public VendorRecoveryPublicKey(DobbyPluginTrustProfile profile = DobbyPluginTrustProfile.Production)
        : this(CreateEmbeddedKeyRing(profile))
    {
    }

    internal VendorRecoveryPublicKey(ArtifactPublicKeyRing keyRing)
    {
        this.keyRing = keyRing ?? throw new ArgumentNullException(nameof(keyRing));
    }

    public bool Verify(
        string keyId,
        string purpose,
        byte[] canonicalPayload,
        byte[] signature,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        ArgumentNullException.ThrowIfNull(signature);
        return keyRing.Verify(keyId, purpose, canonicalPayload, signature, now);
    }

    private static ArtifactPublicKeyRing CreateEmbeddedKeyRing(DobbyPluginTrustProfile profile)
    {
        var production = profile == DobbyPluginTrustProfile.Production;
        var resourceName = production ? ProductionResourceName : DevQaResourceName;
        using var stream = typeof(VendorRecoveryPublicKey).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Purpose-separated recovery trust resource is missing.");
        using var reader = new StreamReader(stream);
        using var key = ECDsa.Create();
        key.ImportFromPem(reader.ReadToEnd());
        if (key.KeySize != 256)
        {
            throw new InvalidOperationException("Recovery trust key must use P-256.");
        }

        return new ArtifactPublicKeyRing(
            [
                new ArtifactVerificationKey(
                    production ? ProductionKeyId : DevQaKeyId,
                    ArtifactSignaturePurposes.OwnerRecoveryResponse,
                    key.ExportSubjectPublicKeyInfo(),
                    DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    RetiredAtUtc: null,
                    IsTestOnly: !production)
            ],
            productionProfile: production);
    }
}

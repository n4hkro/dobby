using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Dobby.Security.Trust;

public sealed class PluginTrustPublicKeyProvider
{
    public const string ProductionResourceName = "Dobby.Security.Trust.vendor-plugin-package-public-key.pem";
    public const string DevQaResourceName = "Dobby.Security.Trust.dev-qa-plugin-package-public-key.pem";
    public const string ProductionLicenseResourceName = "Dobby.Security.Trust.vendor-plugin-license-public-key.pem";
    public const string DevQaLicenseResourceName = "Dobby.Security.Trust.dev-qa-plugin-license-public-key.pem";
    public const string ProductionPluginPackageKeyId = "plugin-prod-2026-01";
    public const string DevQaPluginPackageKeyId = "plugin-test-2026-01";
    public const string ProductionPluginLicenseKeyId = "license-prod-2026-01";
    public const string DevQaPluginLicenseKeyId = "license-test-2026-01";

    private readonly DobbyPluginTrustProfile profile;
    private readonly string purpose;
    private readonly IReadOnlyDictionary<string, byte[]>? resourceMap;
    private readonly Assembly assembly;

    public DobbyPluginTrustProfile Profile => profile;

    public string Purpose => purpose;

    public PluginTrustPublicKeyProvider(
        DobbyPluginTrustProfile profile = DobbyPluginTrustProfile.Production,
        string purpose = ArtifactSignaturePurposes.PluginPackage)
        : this(profile, purpose, assembly: typeof(PluginTrustPublicKeyProvider).Assembly)
    {
    }

    internal PluginTrustPublicKeyProvider(
        DobbyPluginTrustProfile profile,
        IReadOnlyDictionary<string, byte[]> resourceMap,
        string purpose = ArtifactSignaturePurposes.PluginPackage)
    {
        ArgumentNullException.ThrowIfNull(resourceMap);

        this.profile = profile;
        this.purpose = ValidatePurpose(purpose);
        this.resourceMap = resourceMap;
        assembly = typeof(PluginTrustPublicKeyProvider).Assembly;
    }

    private PluginTrustPublicKeyProvider(DobbyPluginTrustProfile profile, string purpose, Assembly assembly)
    {
        this.profile = profile;
        this.purpose = ValidatePurpose(purpose);
        this.assembly = assembly;
    }

    public PluginTrustPublicKeyResolutionResult Resolve()
    {
        var descriptor = ResolveDescriptor(profile, purpose);
        var resourceName = descriptor.ResourceName;

        var resources = resourceMap ?? ReadAssemblyResources();
        var keys = resources.Keys.ToArray();
        var expected = keys.Where(key => key.Equals(resourceName, StringComparison.Ordinal)).ToArray();
        if (expected.Length != 1)
        {
            return PluginTrustPublicKeyResolutionResult.Failure("trust-failed", $"Missing trust resource '{resourceName}'.");
        }

        var forbiddenName = ResolveDescriptor(
            profile == DobbyPluginTrustProfile.Production
                ? DobbyPluginTrustProfile.DevQa
                : DobbyPluginTrustProfile.Production,
            purpose).ResourceName;
        if (resources.ContainsKey(forbiddenName))
        {
            return PluginTrustPublicKeyResolutionResult.Failure("trust-failed", $"Unexpected trust resource '{forbiddenName}' was found alongside the selected profile.");
        }

        var pemBytes = resources[resourceName];
        if (pemBytes.Length == 0)
        {
            return PluginTrustPublicKeyResolutionResult.Failure("trust-failed", $"Trust resource '{resourceName}' is empty.");
        }

        try
        {
            var der = DecodePem(pemBytes);
            return PluginTrustPublicKeyResolutionResult.Success(
                new PluginTrustPublicKey(
                    resourceName,
                    profile,
                    purpose,
                    descriptor.KeyId,
                    der,
                    Sha256(der),
                    DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    RetiredAtUtc: null,
                    IsTestOnly: profile == DobbyPluginTrustProfile.DevQa));
        }
        catch (FormatException exception)
        {
            return PluginTrustPublicKeyResolutionResult.Failure("trust-failed", $"Trust resource '{resourceName}' is malformed: {exception.Message}");
        }
    }

    private IReadOnlyDictionary<string, byte[]> ReadAssemblyResources()
    {
        var names = assembly.GetManifestResourceNames();
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            map[name] = memory.ToArray();
        }

        return map;
    }

    private static byte[] DecodePem(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        const string beginMarker = "-----BEGIN PUBLIC KEY-----";
        const string endMarker = "-----END PUBLIC KEY-----";

        var begin = text.IndexOf(beginMarker, StringComparison.Ordinal);
        var end = text.IndexOf(endMarker, StringComparison.Ordinal);
        if (begin < 0 || end < 0 || end <= begin)
        {
            throw new FormatException("PEM public key boundaries were not found.");
        }

        var base64 = text[(begin + beginMarker.Length)..end]
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();

        return Convert.FromBase64String(base64);
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string ValidatePurpose(string value)
    {
        if (!value.Equals(ArtifactSignaturePurposes.PluginPackage, StringComparison.Ordinal) &&
            !value.Equals(ArtifactSignaturePurposes.PluginLicense, StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Only plugin-package and plugin-license trust purposes are supported.");
        }

        return value;
    }

    private static TrustResourceDescriptor ResolveDescriptor(DobbyPluginTrustProfile profile, string purpose)
    {
        return (profile, purpose) switch
        {
            (DobbyPluginTrustProfile.Production, ArtifactSignaturePurposes.PluginPackage) =>
                new TrustResourceDescriptor(ProductionResourceName, ProductionPluginPackageKeyId),
            (DobbyPluginTrustProfile.DevQa, ArtifactSignaturePurposes.PluginPackage) =>
                new TrustResourceDescriptor(DevQaResourceName, DevQaPluginPackageKeyId),
            (DobbyPluginTrustProfile.Production, ArtifactSignaturePurposes.PluginLicense) =>
                new TrustResourceDescriptor(ProductionLicenseResourceName, ProductionPluginLicenseKeyId),
            (DobbyPluginTrustProfile.DevQa, ArtifactSignaturePurposes.PluginLicense) =>
                new TrustResourceDescriptor(DevQaLicenseResourceName, DevQaPluginLicenseKeyId),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
    }

    private sealed record TrustResourceDescriptor(string ResourceName, string KeyId);
}

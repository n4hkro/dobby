using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Dobby.Security.Trust;

public sealed class PluginTrustPublicKeyProvider
{
    public const string ProductionResourceName = "Dobby.Security.Trust.vendor-master-public-key.pem";
    public const string DevQaResourceName = "Dobby.Security.Trust.dev-qa-public-key.pem";

    private readonly DobbyPluginTrustProfile profile;
    private readonly IReadOnlyDictionary<string, byte[]>? resourceMap;
    private readonly Assembly assembly;

    public PluginTrustPublicKeyProvider(DobbyPluginTrustProfile profile = DobbyPluginTrustProfile.Production)
        : this(profile, assembly: typeof(PluginTrustPublicKeyProvider).Assembly)
    {
    }

    internal PluginTrustPublicKeyProvider(
        DobbyPluginTrustProfile profile,
        IReadOnlyDictionary<string, byte[]> resourceMap)
    {
        ArgumentNullException.ThrowIfNull(resourceMap);

        this.profile = profile;
        this.resourceMap = resourceMap;
        assembly = typeof(PluginTrustPublicKeyProvider).Assembly;
    }

    private PluginTrustPublicKeyProvider(DobbyPluginTrustProfile profile, Assembly assembly)
    {
        this.profile = profile;
        this.assembly = assembly;
    }

    public PluginTrustPublicKeyResolutionResult Resolve()
    {
        var resourceName = profile == DobbyPluginTrustProfile.Production
            ? ProductionResourceName
            : DevQaResourceName;

        var resources = resourceMap ?? ReadAssemblyResources();
        var keys = resources.Keys.ToArray();
        var expected = keys.Where(key => key.Equals(resourceName, StringComparison.Ordinal)).ToArray();
        if (expected.Length != 1)
        {
            return PluginTrustPublicKeyResolutionResult.Failure("trust-failed", $"Missing trust resource '{resourceName}'.");
        }

        var forbiddenName = profile == DobbyPluginTrustProfile.Production ? DevQaResourceName : ProductionResourceName;
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
                    1,
                    der,
                    Sha256(der)));
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
}

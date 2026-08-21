using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dobby.Security.Trust;

public static class ArtifactSignatureProfile
{
    public const string Algorithm = "ECDSA-P256-SHA256";
    public const string Format = "IeeeP1363FixedFieldConcatenation";
    public const int P256SignatureLength = 64;
}

public static class ArtifactSignaturePurposes
{
    public const string PluginPackage = "dobby.plugin-package";
    public const string PluginLicense = "dobby.plugin-license";
    public const string OwnerRecoveryRequest = "dobby.owner-recovery-request";
    public const string OwnerRecoveryRequestPossession = "dobby.owner-recovery-request-possession";
    public const string OwnerRecoveryResponse = "dobby.owner-recovery-response";
    public const string BranchSetupAuthorization = "dobby.branch-setup-authorization";
}

public sealed record ArtifactSignatureEnvelope(
    [property: JsonPropertyName("purpose")]
    string Purpose,
    [property: JsonPropertyName("algorithm")]
    string Algorithm,
    [property: JsonPropertyName("format")]
    string Format,
    [property: JsonPropertyName("keyId")]
    string KeyId,
    [property: JsonPropertyName("value")]
    string Value);

public static class ArtifactSignatureCodec
{
    public static string ToUnpaddedBase64Url(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecodeUnpaddedBase64Url(
        string? value,
        int expectedByteLength,
        out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains('=') ||
            value.Contains('+') ||
            value.Contains('/') ||
            value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            return false;
        }

        var paddingLength = (4 - value.Length % 4) % 4;
        try
        {
            bytes = Convert.FromBase64String(
                value.Replace('-', '+').Replace('_', '/') + new string('=', paddingLength));
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }

        if (bytes.Length != expectedByteLength ||
            !StringComparer.Ordinal.Equals(ToUnpaddedBase64Url(bytes), value))
        {
            bytes = [];
            return false;
        }

        return true;
    }
}

public static class ArtifactCanonicalJson
{
    public static byte[] Write<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteElement(writer, document.RootElement);
        }

        return stream.ToArray();
    }

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value);
                }

                writer.WriteEndObject();
                return;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteElement(writer, item);
                }

                writer.WriteEndArray();
                return;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                return;
            case JsonValueKind.Number:
                if (!element.TryGetInt64(out var integer))
                {
                    throw new InvalidOperationException("Canonical signed JSON permits only integral numeric values.");
                }

                writer.WriteNumberValue(integer);
                return;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                return;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                return;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                return;
            default:
                throw new InvalidOperationException("Unsupported canonical signed JSON value kind.");
        }
    }
}

public static class ArtifactCanonicalPayload
{
    private static readonly byte[] Header = "DOBBY-SIGNED-ARTIFACT\0"u8.ToArray();

    public static byte[] Compose(
        string purpose,
        int schemaVersion,
        ReadOnlySpan<byte> canonicalJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (schemaVersion <= 0 || canonicalJson.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        var purposeBytes = Encoding.UTF8.GetBytes(purpose);
        var versionBytes = Encoding.ASCII.GetBytes(schemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var payload = new byte[
            Header.Length + purposeBytes.Length + 1 + versionBytes.Length + 1 + canonicalJson.Length];
        var offset = 0;
        Header.CopyTo(payload, offset);
        offset += Header.Length;
        purposeBytes.CopyTo(payload, offset);
        offset += purposeBytes.Length;
        payload[offset++] = 0;
        versionBytes.CopyTo(payload, offset);
        offset += versionBytes.Length;
        payload[offset++] = 0;
        canonicalJson.CopyTo(payload.AsSpan(offset));
        return payload;
    }
}

public sealed record ArtifactVerificationKey(
    string KeyId,
    string Purpose,
    byte[] SubjectPublicKeyInfo,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset? RetiredAtUtc,
    bool IsTestOnly);

public sealed class ArtifactPublicKeyRing
{
    private const int MaximumKeyCount = 16;
    private readonly IReadOnlyDictionary<string, ArtifactVerificationKey> keys;
    private readonly bool productionProfile;

    public ArtifactPublicKeyRing(
        IEnumerable<ArtifactVerificationKey> keys,
        bool productionProfile)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var materialized = keys.ToArray();
        if (materialized.Length is 0 or > MaximumKeyCount ||
            materialized.Any(key => string.IsNullOrWhiteSpace(key.KeyId) || key.KeyId.Length > 64) ||
            materialized.Select(key => key.KeyId).Distinct(StringComparer.Ordinal).Count() != materialized.Length ||
            (productionProfile && materialized.Any(key => key.IsTestOnly)))
        {
            throw new InvalidOperationException("Artifact verification key ring is invalid for the selected profile.");
        }

        this.keys = materialized.ToDictionary(key => key.KeyId, StringComparer.Ordinal);
        this.productionProfile = productionProfile;
    }

    public bool Verify(
        string keyId,
        string purpose,
        ReadOnlySpan<byte> canonicalPayload,
        ReadOnlySpan<byte> signature,
        DateTimeOffset now)
    {
        if (!keys.TryGetValue(keyId, out var key) ||
            !StringComparer.Ordinal.Equals(key.Purpose, purpose) ||
            canonicalPayload.IsEmpty ||
            signature.Length != ArtifactSignatureProfile.P256SignatureLength ||
            now.ToUniversalTime() < key.NotBeforeUtc.ToUniversalTime() ||
            (key.RetiredAtUtc.HasValue && now.ToUniversalTime() >= key.RetiredAtUtc.Value.ToUniversalTime()) ||
            (productionProfile && key.IsTestOnly))
        {
            return false;
        }

        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(key.SubjectPublicKeyInfo, out var bytesRead);
            return bytesRead == key.SubjectPublicKeyInfo.Length &&
                verifier.KeySize == 256 &&
                verifier.VerifyData(
                    canonicalPayload,
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}

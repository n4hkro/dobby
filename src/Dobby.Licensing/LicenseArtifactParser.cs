using System.Text;
using System.Text.Json;
using Dobby.Security.Trust;

namespace Dobby.Licensing;

public static class LicenseArtifactParser
{
    private const int MaximumJsonDepth = 16;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static LicenseArtifactParseResult Parse(string fileName, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(bytes);

        if (!Path.GetExtension(fileName).Equals(".dobbylicense", StringComparison.OrdinalIgnoreCase) ||
            bytes.Length is 0 or > LicenseV3Contract.MaximumArtifactBytes ||
            StartsWithBom(bytes))
        {
            return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        try
        {
            _ = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumJsonDepth
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !TryGetExactProperty(document.RootElement, "payload", 2, out var payloadElement) ||
                !TryGetExactProperty(document.RootElement, "signature", 2, out var signatureElement) ||
                payloadElement.ValueKind != JsonValueKind.Object ||
                signatureElement.ValueKind != JsonValueKind.Object)
            {
                return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
            }

            if (!TryReadSignature(signatureElement, out var signature))
            {
                return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidSignature);
            }

            var payload = payloadElement.Clone();
            var canonicalJson = LicenseCanonicalJsonWriter.Write(payload);
            var canonicalPayload = ArtifactCanonicalPayload.Compose(
                ArtifactSignaturePurposes.PluginLicense,
                LicenseV3Contract.SchemaVersion,
                canonicalJson);
            return LicenseArtifactParseResult.Success(new LicenseArtifact(payload, signature, canonicalPayload));
        }
        catch (JsonException)
        {
            return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }
        catch (InvalidOperationException)
        {
            return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }
    }

    private static bool TryReadSignature(JsonElement element, out LicenseSignatureBlock signature)
    {
        signature = new LicenseSignatureBlock(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        var properties = element.EnumerateObject().ToArray();
        if (properties.Length != 5 ||
            properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != 5 ||
            !TryGetString(element, "purpose", out var purpose) ||
            !TryGetString(element, "algorithm", out var algorithm) ||
            !TryGetString(element, "format", out var format) ||
            !TryGetString(element, "keyId", out var keyId) ||
            !TryGetString(element, "value", out var value) ||
            !purpose.Equals(ArtifactSignaturePurposes.PluginLicense, StringComparison.Ordinal) ||
            !algorithm.Equals(ArtifactSignatureProfile.Algorithm, StringComparison.Ordinal) ||
            !format.Equals(ArtifactSignatureProfile.Format, StringComparison.Ordinal) ||
            keyId.Length is < 3 or > 64 ||
            !ArtifactSignatureCodec.TryDecodeUnpaddedBase64Url(
                value,
                ArtifactSignatureProfile.P256SignatureLength,
                out _))
        {
            return false;
        }

        signature = new LicenseSignatureBlock(purpose, algorithm, format, keyId, value);
        return true;
    }

    private static bool TryGetExactProperty(
        JsonElement element,
        string name,
        int expectedTotalProperties,
        out JsonElement value)
    {
        value = default;
        var properties = element.EnumerateObject().ToArray();
        var matches = properties.Where(property => property.NameEquals(name)).ToArray();
        if (properties.Length != expectedTotalProperties ||
            properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != expectedTotalProperties ||
            matches.Length != 1)
        {
            return false;
        }

        value = matches[0].Value;
        return true;
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        var matches = element.EnumerateObject().Where(property => property.NameEquals(name)).ToArray();
        if (matches.Length != 1 || matches[0].Value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = matches[0].Value.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool StartsWithBom(byte[] bytes)
    {
        var bom = Encoding.UTF8.GetPreamble();
        return bytes.Length >= bom.Length && bytes.AsSpan(0, bom.Length).SequenceEqual(bom);
    }
}

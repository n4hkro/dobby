using System.Text;
using System.Text.Json;

namespace Dobby.Licensing;

public static class LicenseArtifactParser
{
    private const string ExpectedAlgorithm = "ECDSA-P256-SHA256";
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static LicenseArtifactParseResult Parse(string fileName, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(bytes);

        if (!Path.GetExtension(fileName).Equals(".dobbylicense", StringComparison.OrdinalIgnoreCase))
        {
            return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        if (StartsWithBom(bytes))
        {
            return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        string json;
        try
        {
            json = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
            }

            var rootProperties = document.RootElement.EnumerateObject().ToArray();
            if (rootProperties.Length != 2)
            {
                return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
            }

            var payloadProperties = rootProperties
                .Where(property => property.NameEquals("payload"))
                .ToArray();
            var signatureProperties = rootProperties
                .Where(property => property.NameEquals("signature"))
                .ToArray();

            if (payloadProperties.Length != 1 ||
                signatureProperties.Length != 1 ||
                payloadProperties[0].Value.ValueKind != JsonValueKind.Object ||
                signatureProperties[0].Value.ValueKind != JsonValueKind.Object)
            {
                return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
            }

            if (!TryReadSignature(signatureProperties[0].Value, out var signature))
            {
                return LicenseArtifactParseResult.Failure(LicenseRejectReasons.InvalidSignature);
            }

            var payload = payloadProperties[0].Value.Clone();
            var canonicalPayload = LicenseCanonicalJsonWriter.Write(payload);
            return LicenseArtifactParseResult.Success(new LicenseArtifact(payload, signature, canonicalPayload));
        }
    }

    private static bool TryReadSignature(JsonElement signatureElement, out LicenseSignatureBlock signature)
    {
        signature = new LicenseSignatureBlock(string.Empty, 0, string.Empty);
        var properties = signatureElement.EnumerateObject().ToArray();
        if (properties.Length != 3)
        {
            return false;
        }

        var algorithmProperties = properties
            .Where(property => property.NameEquals("algorithm"))
            .ToArray();
        var keyVersionProperties = properties
            .Where(property => property.NameEquals("keyVersion"))
            .ToArray();
        var valueProperties = properties
            .Where(property => property.NameEquals("value"))
            .ToArray();

        if (algorithmProperties.Length != 1 ||
            keyVersionProperties.Length != 1 ||
            valueProperties.Length != 1)
        {
            return false;
        }

        if (algorithmProperties[0].Value.ValueKind != JsonValueKind.String ||
            !algorithmProperties[0].Value.GetString()!.Equals(ExpectedAlgorithm, StringComparison.Ordinal))
        {
            return false;
        }

        if (keyVersionProperties[0].Value.ValueKind != JsonValueKind.Number ||
            !keyVersionProperties[0].Value.TryGetInt32(out var keyVersion) ||
            keyVersion != 1)
        {
            return false;
        }

        if (valueProperties[0].Value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = valueProperties[0].Value.GetString()!;
        if (!IsBase64UrlNoPadding(value))
        {
            return false;
        }

        signature = new LicenseSignatureBlock(ExpectedAlgorithm, keyVersion, value);
        return true;
    }

    private static bool IsBase64UrlNoPadding(string value)
    {
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
            return Base64UrlDecode(value).Length > 0;
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

    private static bool StartsWithBom(byte[] bytes)
    {
        var bom = Encoding.UTF8.GetPreamble();
        return bytes.Length >= bom.Length && bytes.AsSpan(0, bom.Length).SequenceEqual(bom);
    }
}

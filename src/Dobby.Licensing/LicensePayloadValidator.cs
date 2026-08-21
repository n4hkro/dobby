using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dobby.Security.Trust;

namespace Dobby.Licensing;

public static class LicensePayloadValidator
{
    private static readonly string[] RequiredProperties =
    [
        "schemaVersion", "purpose", "keyId", "licenseId", "revisionSeriesId",
        "licenseRevision", "pluginId", "customerId", "deploymentId", "deviceKeyHash",
        "packageFingerprint", "issuedAt", "notBefore", "expiresAt", "gracePeriodDays", "intent"
    ];

    private static readonly HashSet<string> RequiredPropertySet = RequiredProperties.ToHashSet(StringComparer.Ordinal);
    private static readonly Regex IdentifierPattern = new("^[A-Za-z0-9][A-Za-z0-9._:-]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PluginIdPattern = new("^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex KeyIdPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex UtcDateTimePattern = new(
        "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\\.[0-9]{1,7})?Z$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static LicensePayloadValidationResult Validate(LicenseArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        if (artifact.Payload.ValueKind != JsonValueKind.Object || !HasExactPayloadShape(artifact.Payload) ||
            !TryGetRequiredInt(artifact.Payload, "schemaVersion", out var schemaVersion) ||
            schemaVersion != LicenseV3Contract.SchemaVersion ||
            !TryGetRequiredString(artifact.Payload, "purpose", out var purpose) ||
            !purpose.Equals(ArtifactSignaturePurposes.PluginLicense, StringComparison.Ordinal) ||
            !TryGetRequiredString(artifact.Payload, "keyId", out var keyId) ||
            !TryGetRequiredString(artifact.Payload, "licenseId", out var licenseId) ||
            !TryGetRequiredString(artifact.Payload, "revisionSeriesId", out var revisionSeriesId) ||
            !TryGetRequiredInt(artifact.Payload, "licenseRevision", out var licenseRevision) ||
            !TryGetRequiredString(artifact.Payload, "pluginId", out var pluginId) ||
            !TryGetRequiredString(artifact.Payload, "customerId", out var customerId) ||
            !TryGetRequiredString(artifact.Payload, "deploymentId", out var deploymentId) ||
            !TryGetRequiredString(artifact.Payload, "deviceKeyHash", out var deviceKeyHash) ||
            !TryGetRequiredString(artifact.Payload, "packageFingerprint", out var packageFingerprint) ||
            !TryGetRequiredString(artifact.Payload, "issuedAt", out var issuedAtRaw) ||
            !TryGetRequiredString(artifact.Payload, "notBefore", out var notBeforeRaw) ||
            !TryGetRequiredString(artifact.Payload, "expiresAt", out var expiresAtRaw) ||
            !TryGetRequiredInt(artifact.Payload, "gracePeriodDays", out var gracePeriodDays) ||
            !TryGetRequiredString(artifact.Payload, "intent", out var intentRaw))
        {
            return LicensePayloadValidationResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        if (!keyId.Equals(artifact.Signature.KeyId, StringComparison.Ordinal))
        {
            return LicensePayloadValidationResult.Failure(LicenseRejectReasons.InvalidSignature);
        }

        if (!IsBoundedIdentifier(keyId, 64, KeyIdPattern) ||
            !IsBoundedIdentifier(licenseId, LicenseV3Contract.MaximumIdentifierLength, IdentifierPattern) ||
            !IsBoundedIdentifier(revisionSeriesId, LicenseV3Contract.MaximumIdentifierLength, IdentifierPattern) ||
            !IsBoundedIdentifier(pluginId, LicenseV3Contract.MaximumIdentifierLength, PluginIdPattern) ||
            !IsBoundedIdentifier(customerId, LicenseV3Contract.MaximumIdentifierLength, IdentifierPattern) ||
            !IsBoundedIdentifier(deploymentId, LicenseV3Contract.MaximumIdentifierLength, IdentifierPattern) ||
            !IsLowercaseSha256(deviceKeyHash) ||
            !IsLowercaseSha256(packageFingerprint) ||
            licenseRevision < 1 ||
            gracePeriodDays is < 0 or > LicenseV3Contract.MaximumGracePeriodDays ||
            !TryMapIntent(intentRaw, out var intent) ||
            !TryParseUtc(issuedAtRaw, out var issuedAt) ||
            !TryParseUtc(notBeforeRaw, out var notBefore) ||
            !TryParseUtc(expiresAtRaw, out var expiresAt) ||
            issuedAt > notBefore ||
            notBefore > expiresAt ||
            !CanAddGrace(expiresAt, gracePeriodDays))
        {
            return LicensePayloadValidationResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        return LicensePayloadValidationResult.Success(new LicensePayload(
            schemaVersion,
            purpose,
            keyId,
            licenseId,
            revisionSeriesId,
            licenseRevision,
            pluginId,
            customerId,
            deploymentId,
            deviceKeyHash,
            packageFingerprint,
            issuedAt,
            notBefore,
            expiresAt,
            gracePeriodDays,
            intent));
    }

    private static bool HasExactPayloadShape(JsonElement payload)
    {
        var names = payload.EnumerateObject().Select(property => property.Name).ToArray();
        return names.Length == RequiredProperties.Length &&
            names.Distinct(StringComparer.Ordinal).Count() == RequiredProperties.Length &&
            names.All(RequiredPropertySet.Contains);
    }

    private static bool TryGetRequiredString(JsonElement payload, string name, out string value)
    {
        value = string.Empty;
        var matches = payload.EnumerateObject().Where(property => property.NameEquals(name)).ToArray();
        if (matches.Length != 1 || matches[0].Value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = matches[0].Value.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryGetRequiredInt(JsonElement payload, string name, out int value)
    {
        value = 0;
        var matches = payload.EnumerateObject().Where(property => property.NameEquals(name)).ToArray();
        return matches.Length == 1 &&
            matches[0].Value.ValueKind == JsonValueKind.Number &&
            matches[0].Value.TryGetInt32(out value);
    }

    private static bool IsBoundedIdentifier(string value, int maximum, Regex pattern)
    {
        return value.Length <= maximum && pattern.IsMatch(value) && !value.Any(char.IsControl);
    }

    private static bool IsLowercaseSha256(string value)
    {
        return value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static bool TryMapIntent(string value, out LicenseGrantIntent intent)
    {
        intent = value switch
        {
            "granted" => LicenseGrantIntent.Granted,
            "revoked" => LicenseGrantIntent.Revoked,
            _ => (LicenseGrantIntent)(-1)
        };
        return intent != (LicenseGrantIntent)(-1);
    }

    private static bool TryParseUtc(string value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (value.Length > 35 || !UtcDateTimePattern.IsMatch(value))
        {
            return false;
        }

        return DateTimeOffset.TryParseExact(
            value,
            ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestamp);
    }

    private static bool CanAddGrace(DateTimeOffset expiresAt, int gracePeriodDays)
    {
        try
        {
            _ = expiresAt.AddDays(gracePeriodDays);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}

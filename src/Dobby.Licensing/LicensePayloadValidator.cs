using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dobby.Licensing;

public static class LicensePayloadValidator
{
    private static readonly string[] RequiredProperties =
    [
        "schemaVersion",
        "licenseId",
        "pluginId",
        "customerId",
        "deploymentId",
        "deviceFingerprintAlgorithm",
        "deviceFingerprint",
        "packageFingerprint",
        "licenseRevision",
        "issuedAt",
        "notBefore",
        "expiresAt",
        "gracePeriodDays",
        "status",
        "keyVersion"
    ];

    private static readonly HashSet<string> RequiredPropertySet = RequiredProperties.ToHashSet(StringComparer.Ordinal);
    private static readonly Regex PluginIdPattern = new("^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$", RegexOptions.CultureInvariant);
    private static readonly Regex UtcDateTimePattern = new("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\\.[0-9]+)?Z$", RegexOptions.CultureInvariant);

    public static LicensePayloadValidationResult Validate(LicenseArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        if (artifact.Payload.ValueKind != JsonValueKind.Object ||
            !HasExactPayloadShape(artifact.Payload))
        {
            return LicensePayloadValidationResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        if (!TryGetRequiredInt(artifact.Payload, "schemaVersion", out var schemaVersion) || schemaVersion != 2 ||
            !TryGetRequiredString(artifact.Payload, "licenseId", out var licenseId) ||
            !TryGetRequiredString(artifact.Payload, "pluginId", out var pluginId) ||
            !TryGetRequiredString(artifact.Payload, "customerId", out var customerId) ||
            !TryGetRequiredString(artifact.Payload, "deploymentId", out var deploymentId) ||
            !TryGetRequiredString(artifact.Payload, "deviceFingerprintAlgorithm", out var deviceFingerprintAlgorithm) ||
            !TryGetRequiredString(artifact.Payload, "deviceFingerprint", out var deviceFingerprint) ||
            !TryGetRequiredString(artifact.Payload, "packageFingerprint", out var packageFingerprint) ||
            !TryGetRequiredInt(artifact.Payload, "licenseRevision", out var licenseRevision) ||
            !TryGetRequiredString(artifact.Payload, "issuedAt", out var issuedAtRaw) ||
            !TryGetRequiredString(artifact.Payload, "notBefore", out var notBeforeRaw) ||
            !TryGetRequiredString(artifact.Payload, "expiresAt", out var expiresAtRaw) ||
            !TryGetRequiredInt(artifact.Payload, "gracePeriodDays", out var gracePeriodDays) ||
            !TryGetRequiredString(artifact.Payload, "status", out var statusRaw) ||
            !TryGetRequiredInt(artifact.Payload, "keyVersion", out var keyVersion))
        {
            return LicensePayloadValidationResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        if (keyVersion != 1 || keyVersion != artifact.Signature.KeyVersion)
        {
            return LicensePayloadValidationResult.Failure(LicenseRejectReasons.InvalidSignature);
        }

        if (!PluginIdPattern.IsMatch(pluginId) ||
            !deviceFingerprintAlgorithm.Equals("v1", StringComparison.Ordinal) ||
            !IsLowercaseSha256(deviceFingerprint) ||
            !IsLowercaseSha256(packageFingerprint) ||
            licenseRevision < 1 ||
            gracePeriodDays < 0 ||
            !TryMapStatus(statusRaw, out var status) ||
            !TryParseUtc(issuedAtRaw, out var issuedAt) ||
            !TryParseUtc(notBeforeRaw, out var notBefore) ||
            !TryParseUtc(expiresAtRaw, out var expiresAt) ||
            issuedAt > notBefore ||
            notBefore > expiresAt)
        {
            return LicensePayloadValidationResult.Failure(LicenseRejectReasons.InvalidLicenseFile);
        }

        return LicensePayloadValidationResult.Success(new LicensePayload(
            schemaVersion,
            licenseId,
            pluginId,
            customerId,
            deploymentId,
            deviceFingerprintAlgorithm,
            deviceFingerprint,
            packageFingerprint,
            licenseRevision,
            issuedAt,
            notBefore,
            expiresAt,
            gracePeriodDays,
            status,
            keyVersion));
    }

    private static bool HasExactPayloadShape(JsonElement payload)
    {
        var names = payload.EnumerateObject()
            .Select(property => property.Name)
            .ToArray();

        return names.Length == RequiredProperties.Length &&
            names.Distinct(StringComparer.Ordinal).Count() == RequiredProperties.Length &&
            names.All(name => RequiredPropertySet.Contains(name));
    }

    private static bool TryGetRequiredString(JsonElement payload, string name, out string value)
    {
        value = string.Empty;
        if (!payload.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryGetRequiredInt(JsonElement payload, string name, out int value)
    {
        value = 0;
        return payload.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static bool IsLowercaseSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        return value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static bool TryMapStatus(string value, out LicenseArtifactStatus status)
    {
        status = value switch
        {
            "active" => LicenseArtifactStatus.Active,
            "pending" => LicenseArtifactStatus.Pending,
            "readonly" => LicenseArtifactStatus.ReadOnly,
            "denied" => LicenseArtifactStatus.Denied,
            _ => (LicenseArtifactStatus)(-1)
        };

        return status != (LicenseArtifactStatus)(-1);
    }

    private static bool TryParseUtc(string value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (!UtcDateTimePattern.IsMatch(value))
        {
            return false;
        }

        var formats = new[]
        {
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"
        };

        return DateTimeOffset.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestamp);
    }
}

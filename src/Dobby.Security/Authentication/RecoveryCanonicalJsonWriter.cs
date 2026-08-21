using Dobby.Security.Trust;

namespace Dobby.Security.Authentication;

public static class RecoveryCanonicalJsonWriter
{
    public static byte[] WriteRequestPayload(RecoveryRequestPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return ArtifactCanonicalJson.Write(payload);
    }

    public static byte[] WritePayload(RecoveryResponsePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return ArtifactCanonicalJson.Write(payload);
    }
}

internal static class RecoveryContractValidation
{
    public static bool IsBoundedIdentifier(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Length <= 128 &&
            char.IsAsciiLetterOrDigit(value[0]) &&
            value.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '_' or ':' or '-');
    }

    public static bool IsKeyId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Length is >= 3 and <= 64 &&
            char.IsAsciiLetterOrDigit(value[0]) &&
            value.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '_' or '-');
    }

    public static bool IsLowercaseSha256(string? value)
    {
        return value is { Length: 64 } &&
            value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    public static bool IsUuid(string? value)
    {
        return Guid.TryParseExact(value, "D", out var parsed) &&
            StringComparer.Ordinal.Equals(parsed.ToString("D"), value);
    }

    public static bool TryParseUtcZ(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        return value is { Length: <= 35 } &&
            value.EndsWith('Z') &&
            DateTimeOffset.TryParseExact(
                value,
                ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out timestamp);
    }

    public static string ToUtcZ(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }
}

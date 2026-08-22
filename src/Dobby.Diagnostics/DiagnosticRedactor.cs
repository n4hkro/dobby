using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dobby.Diagnostics;

public static partial class DiagnosticRedactor
{
    private const string RedactedValue = "<redacted>";
    private const string OversizedValue = "<redacted:oversized>";
    private const int MaximumTextLength = 16 * 1024;
    private const int MaximumFieldCount = 32;
    private const int MaximumFieldValueLength = 4096;

    private static readonly string[] SensitiveFieldFragments =
    [
        "password",
        "token",
        "secret",
        "privatekey",
        "signature",
        "devicefingerprint",
        "session",
        "remember",
        "entropy",
        "recovery",
        "licensepayload",
        "licenseartifact",
        "licensebinding",
        "connectionstring",
        "authorization",
        "bearer",
        "cookie",
        "apikey"
    ];

    public static string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumTextLength)
        {
            return OversizedValue;
        }

        var normalized = text.Replace('\u2028', '\n').Replace('\u2029', '\n');
        if (TryRedactStructuredJson(normalized, out var structured))
        {
            return structured;
        }

        var redacted = PrivateKeyPemPattern().Replace(normalized, RedactedValue);
        redacted = SensitiveAssignmentPattern().Replace(
            redacted,
            match => $"{match.Groups["key"].Value}{match.Groups["separator"].Value}{RedactedValue}");
        redacted = StackTraceLinePattern().Replace(redacted, RedactedValue);
        redacted = SqlLinePattern().Replace(redacted, RedactedValue);
        redacted = UncPathPattern().Replace(redacted, "<path>");
        redacted = WindowsPathPattern().Replace(redacted, "<path>");
        return redacted;
    }

    public static IReadOnlyDictionary<string, string> RedactFields(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count > MaximumFieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(fields));
        }

        return fields.ToImmutableDictionary(
            static pair => pair.Key,
            static pair => IsSensitiveFieldName(pair.Key)
                ? RedactedValue
                : pair.Value is null || pair.Value.Length > MaximumFieldValueLength
                    ? OversizedValue
                    : Redact(pair.Value),
            StringComparer.Ordinal);
    }

    private static bool TryRedactStructuredJson(string text, out string redacted)
    {
        redacted = string.Empty;
        var trimmed = text.AsSpan().TrimStart();
        if (trimmed.IsEmpty || trimmed[0] is not ('{' or '['))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32
            });
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteJson(writer, document.RootElement);
            }

            redacted = Encoding.UTF8.GetString(stream.ToArray());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void WriteJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (IsSensitiveFieldName(property.Name))
                    {
                        writer.WriteStringValue(RedactedValue);
                    }
                    else
                    {
                        WriteJson(writer, property.Value);
                    }
                }

                writer.WriteEndObject();
                return;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteJson(writer, item);
                }

                writer.WriteEndArray();
                return;
            case JsonValueKind.String:
                writer.WriteStringValue(Redact(element.GetString() ?? string.Empty));
                return;
            default:
                element.WriteTo(writer);
                return;
        }
    }

    private static bool IsSensitiveFieldName(string name)
    {
        var normalized = new string(name
            .Where(character => char.IsLetterOrDigit(character))
            .Select(char.ToLowerInvariant)
            .ToArray());
        return SensitiveFieldFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
    }

    [GeneratedRegex("-----BEGIN(?: [A-Z0-9]+)* PRIVATE KEY-----[\\s\\S]*?-----END(?: [A-Z0-9]+)* PRIVATE KEY-----", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyPemPattern();

    [GeneratedRegex("(?<key>password|passwordHash|token|secret|privateKey|signature|deviceFingerprint|session|sessionMaterial|rememberMe|rememberMeSecret|entropy|recovery|recoverySecret|recoveryNonce|recoveryResponse|licensePayload|licenseArtifact|licenseBinding|connectionString|data source|initial catalog|user id|uid|pwd|authorization|bearer|cookie|apiKey)(?<separator>\\s*[:=]\\s*)(?:\"[^\"\\r\\n]*\"|'[^'\\r\\n]*'|[^;\\s,\\r\\n}]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentPattern();

    [GeneratedRegex(@"(?m)^\s*at\s+.+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StackTraceLinePattern();

    [GeneratedRegex(@"(?im)^\s*(?:SELECT\b.+\bFROM\b|INSERT\s+INTO\b|UPDATE\b.+\bSET\b|DELETE\s+FROM\b|(?:ALTER|CREATE|DROP)\s+(?:TABLE|INDEX)\b|PRAGMA\s+\w+\s*(?:=|;)).*$", RegexOptions.CultureInvariant)]
    private static partial Regex SqlLinePattern();

    [GeneratedRegex(@"\\\\[^\s\""'<>|]+", RegexOptions.CultureInvariant)]
    private static partial Regex UncPathPattern();

    [GeneratedRegex(@"\b[A-Za-z]:[\\/][^\s\""'<>|]+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPathPattern();
}

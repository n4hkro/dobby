using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Dobby.Diagnostics;

public static partial class DiagnosticRedactor
{
    private const string RedactedValue = "<redacted>";

    private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwordHash",
        "token",
        "privateKey",
        "signature",
        "deviceFingerprint",
        "session",
        "sessionMaterial",
        "rememberMe",
        "rememberMeSecret",
        "entropy",
        "recovery",
        "recoverySecret",
        "recoveryNonce",
        "recoveryResponse",
        "licensePayload",
        "licenseArtifact",
        "licenseBinding"
    };

    public static string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return SensitiveAssignmentPattern().Replace(text, match => $"{match.Groups["key"].Value}={RedactedValue}");
    }

    public static IReadOnlyDictionary<string, string> RedactFields(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return fields.ToImmutableDictionary(
            static pair => pair.Key,
            static pair => SensitiveFieldNames.Contains(pair.Key) ? RedactedValue : Redact(pair.Value),
            StringComparer.Ordinal);
    }

    [GeneratedRegex("(?<key>password|passwordHash|token|privateKey|signature|deviceFingerprint|session|sessionMaterial|rememberMe|rememberMeSecret|entropy|recovery|recoverySecret|recoveryNonce|recoveryResponse|licensePayload|licenseArtifact|licenseBinding)=[^;\\s]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentPattern();
}

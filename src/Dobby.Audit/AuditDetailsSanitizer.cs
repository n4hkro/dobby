using System.Text.RegularExpressions;

namespace Dobby.Audit;

internal static partial class AuditDetailsSanitizer
{
    private static readonly string[] ForbiddenKeyFragments =
    [
        "password",
        "passwordHash",
        "token",
        "privateKey",
        "signature",
        "session",
        "sessionMaterial",
        "rememberMe",
        "rememberMeSecret",
        "recovery",
        "recoverySecret",
        "recoveryNonce",
        "recoveryResponse",
        "licensePayload",
        "licenseArtifact",
        "licenseBinding"
    ];

    public static IReadOnlyDictionary<string, string>? Sanitize(IReadOnlyDictionary<string, string>? details)
    {
        if (details is null)
        {
            return null;
        }

        var sanitized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var detail in details)
        {
            EnsureSafe(detail.Key, detail.Value);
            sanitized[detail.Key] = detail.Value;
        }

        return sanitized;
    }

    private static void EnsureSafe(string key, string value)
    {
        if (ForbiddenKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Audit details cannot persist raw secret detail key '{key}'.");
        }

        if (SensitiveAssignmentPattern().IsMatch(value))
        {
            throw new InvalidOperationException($"Audit details cannot persist raw secret detail value for key '{key}'.");
        }
    }

    [GeneratedRegex("(?<key>password|passwordHash|token|privateKey|signature|session|sessionMaterial|rememberMe|rememberMeSecret|recovery|recoverySecret|recoveryNonce|recoveryResponse|licensePayload|licenseArtifact|licenseBinding)\\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentPattern();
}

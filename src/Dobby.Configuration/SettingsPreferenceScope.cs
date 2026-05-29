using System.Security.Cryptography;
using System.Text;

namespace Dobby.Configuration;

public sealed record SettingsPreferenceScope(string KeyPrefix)
{
    public static SettingsPreferenceScope ForAuthenticatedUser(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var normalized = userId.Normalize(NormalizationForm.FormC);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var namespaceId = Convert.ToHexString(hash).ToLowerInvariant()[..32];
        return new SettingsPreferenceScope($"settings.user.{namespaceId}");
    }
}

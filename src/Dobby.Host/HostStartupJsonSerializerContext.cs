using System.Text.Json.Serialization;

namespace Dobby.Host;

[JsonSerializable(typeof(StartupThemeSnapshot))]
[JsonSerializable(typeof(StartupRememberMeSnapshot))]
internal sealed partial class HostStartupJsonSerializerContext : JsonSerializerContext
{
}

internal sealed record StartupThemeSnapshot(string Theme);

internal sealed record StartupRememberMeSnapshot(
    string UserId,
    string MaskedUsername,
    string ProtectedPasswordBlob,
    bool HasMaskedPasswordPrefill);

using System.Text.Json.Serialization;

namespace Dobby.Host;

[JsonSerializable(typeof(StartupThemeSnapshot))]
internal sealed partial class HostStartupJsonSerializerContext : JsonSerializerContext
{
}

internal sealed record StartupThemeSnapshot(string Theme);

namespace Dobby.Configuration;

public sealed class PluginsOptions
{
    public bool AllowPreLoginDiscovery { get; init; }

    public int MaximumCompressedPackageMegabytes { get; init; } = 64;

    public int MaximumManifestKilobytes { get; init; } = 256;

    public int MaximumEntryCount { get; init; } = 256;

    public int MaximumEntryUncompressedMegabytes { get; init; } = 64;

    public int MaximumAggregateUncompressedMegabytes { get; init; } = 256;

    public int MaximumExpansionRatio { get; init; } = 50;
}

namespace Dobby.PluginFramework.Packaging;

public sealed record PluginPackageLimits(
    long MaximumCompressedPackageBytes,
    int MaximumManifestBytes,
    int MaximumEntryCount,
    long MaximumEntryUncompressedBytes,
    long MaximumAggregateUncompressedBytes,
    int MaximumExpansionRatio)
{
    public const long HardMaximumCompressedPackageBytes = 512L * 1024 * 1024;
    public const int HardMaximumManifestBytes = 1024 * 1024;
    public const int HardMaximumEntryCount = 2048;
    public const long HardMaximumEntryUncompressedBytes = 512L * 1024 * 1024;
    public const long HardMaximumAggregateUncompressedBytes = 2L * 1024 * 1024 * 1024;
    public const int HardMaximumExpansionRatio = 200;

    public static PluginPackageLimits Default { get; } = new(
        64L * 1024 * 1024,
        256 * 1024,
        256,
        64L * 1024 * 1024,
        256L * 1024 * 1024,
        50);

    public void Validate()
    {
        if (MaximumCompressedPackageBytes is <= 0 or > HardMaximumCompressedPackageBytes ||
            MaximumManifestBytes is <= 0 or > HardMaximumManifestBytes ||
            MaximumEntryCount is <= 0 or > HardMaximumEntryCount ||
            MaximumEntryUncompressedBytes is <= 0 or > HardMaximumEntryUncompressedBytes ||
            MaximumAggregateUncompressedBytes is <= 0 or > HardMaximumAggregateUncompressedBytes ||
            MaximumAggregateUncompressedBytes < MaximumEntryUncompressedBytes ||
            MaximumExpansionRatio is <= 0 or > HardMaximumExpansionRatio)
        {
            throw new InvalidOperationException("Plugin package limits are invalid or exceed hard safety ceilings.");
        }
    }
}

public sealed record PluginPackageReadProgress(
    int EntriesCompleted,
    int TotalEntries,
    long UncompressedBytesRead,
    long TotalUncompressedBytes);

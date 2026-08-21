using Dobby.PluginFramework.Manifest;

namespace Dobby.PluginFramework.Packaging;

public sealed record PluginPackageArchive(
    string PackagePath,
    string ManifestPath,
    PluginManifest Manifest,
    IReadOnlyList<PluginPackageEntry> Entries,
    string ManifestJson);

public sealed class PluginPackageEntry
{
    private readonly byte[]? inMemoryContent;
    private readonly Func<Stream>? openRead;

    public PluginPackageEntry(
        string path,
        byte[] content,
        string sha256,
        bool isSymlink)
        : this(
            path,
            sha256,
            isSymlink,
            content?.LongLength ?? throw new ArgumentNullException(nameof(content)),
            content.LongLength,
            () => new MemoryStream(content, writable: false),
            content)
    {
    }

    internal PluginPackageEntry(
        string path,
        string sha256,
        bool isSymlink,
        long uncompressedLength,
        long compressedLength,
        Func<Stream> openRead)
        : this(path, sha256, isSymlink, uncompressedLength, compressedLength, openRead, null)
    {
    }

    private PluginPackageEntry(
        string path,
        string sha256,
        bool isSymlink,
        long uncompressedLength,
        long compressedLength,
        Func<Stream> openRead,
        byte[]? inMemoryContent)
    {
        Path = path;
        Sha256 = sha256;
        IsSymlink = isSymlink;
        UncompressedLength = uncompressedLength;
        CompressedLength = compressedLength;
        this.openRead = openRead;
        this.inMemoryContent = inMemoryContent;
    }

    public string Path { get; }
    public string Sha256 { get; }
    public bool IsSymlink { get; }
    public long UncompressedLength { get; }
    public long CompressedLength { get; }

    public byte[] Content => inMemoryContent ?? [];

    public Stream OpenRead()
    {
        return openRead?.Invoke() ?? throw new InvalidOperationException("Package entry content source is unavailable.");
    }
}

public sealed record PluginPackageDescriptor(
    string PackagePath,
    string Fingerprint,
    byte[] CanonicalPayload,
    PluginManifest Manifest,
    IReadOnlyList<PluginPackageEntry> Entries,
    string ManifestPath,
    string ManifestJson);

public sealed record PluginPackageValidationOutcome(
    bool IsSuccess,
    PluginPackageDescriptor? Descriptor,
    IReadOnlyList<PluginPackageValidationReason> Reasons)
{
    public static PluginPackageValidationOutcome Success(PluginPackageDescriptor descriptor)
    {
        return new PluginPackageValidationOutcome(true, descriptor, []);
    }

    public static PluginPackageValidationOutcome Failure(IReadOnlyList<PluginPackageValidationReason> reasons)
    {
        return new PluginPackageValidationOutcome(false, null, reasons);
    }
}

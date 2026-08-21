using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Core;
using Dobby.PluginFramework.Manifest;

namespace Dobby.PluginFramework.Packaging;

public static class PluginPackageReader
{
    public const string ManifestFileName = "dobby-plugin.manifest.json";

    public static Result<PluginPackageArchive> Read(
        string packagePath,
        PluginPackageLimits? limits = null,
        CancellationToken cancellationToken = default,
        IProgress<PluginPackageReadProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || (!File.Exists(packagePath) && !Directory.Exists(packagePath)))
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        try
        {
            var effectiveLimits = limits ?? PluginPackageLimits.Default;
            effectiveLimits.Validate();
            if (Directory.Exists(packagePath))
            {
                return ReadDirectory(packagePath, effectiveLimits, cancellationToken, progress);
            }

            return ReadArchive(packagePath, effectiveLimits, cancellationToken, progress);
        }
        catch (InvalidDataException)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }
        catch (JsonException)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.SerializationFailed);
        }
        catch (DecoderFallbackException)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }
        catch (IOException)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.IoFailed);
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.IoFailed);
        }
        catch (OverflowException)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }
    }

    private static Result<PluginPackageArchive> ReadArchive(
        string packagePath,
        PluginPackageLimits limits,
        CancellationToken cancellationToken,
        IProgress<PluginPackageReadProgress>? progress)
    {
        var packageInfo = new FileInfo(packagePath);
        if (packageInfo.Length > limits.MaximumCompressedPackageBytes)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count is 0 || archive.Entries.Count > limits.MaximumEntryCount)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        var pending = new List<PendingEntry>(archive.Entries.Count);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!PluginPackagePathPolicy.TryNormalize(entry.FullName, out var normalizedPath))
            {
                return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
            }

            var rawPath = entry.FullName;
            pending.Add(new PendingEntry(
                normalizedPath,
                entry.Length,
                entry.CompressedLength,
                IsSymlink(entry),
                () => OpenArchiveEntry(packagePath, rawPath)));
        }

        return ReadEntries(packagePath, pending, limits, cancellationToken, progress);
    }

    private static Result<PluginPackageArchive> ReadDirectory(
        string packagePath,
        PluginPackageLimits limits,
        CancellationToken cancellationToken,
        IProgress<PluginPackageReadProgress>? progress)
    {
        if ((File.GetAttributes(packagePath) & FileAttributes.ReparsePoint) != 0)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        var pending = new List<PendingEntry>();
        var directories = new Stack<string>();
        directories.Push(packagePath);
        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = directories.Pop();
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
                }

                directories.Push(directory);
            }

            foreach (var path in Directory.EnumerateFiles(current))
            {
                if (pending.Count >= limits.MaximumEntryCount)
                {
                    return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
                }

                var relativePath = Path.GetRelativePath(packagePath, path).Replace('\\', '/');
                if (!PluginPackagePathPolicy.TryNormalize(relativePath, out var normalizedPath))
                {
                    return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
                }

                var fileInfo = new FileInfo(path);
                var sourcePath = path;
                pending.Add(new PendingEntry(
                    normalizedPath,
                    fileInfo.Length,
                    fileInfo.Length,
                    (fileInfo.Attributes & FileAttributes.ReparsePoint) != 0,
                    () => new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read)));
            }
        }

        return pending.Count == 0
            ? Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput)
            : ReadEntries(packagePath, pending, limits, cancellationToken, progress);
    }

    private static Result<PluginPackageArchive> ReadEntries(
        string packagePath,
        IReadOnlyList<PendingEntry> pending,
        PluginPackageLimits limits,
        CancellationToken cancellationToken,
        IProgress<PluginPackageReadProgress>? progress)
    {
        if (pending.Any(entry => entry.IsSymlink) ||
            PluginPackagePathPolicy.HasCaseOrUnicodeCollision(pending.Select(entry => entry.Path)) ||
            PluginPackagePathPolicy.HasFileDirectoryPrefixCollision(pending.Select(entry => entry.Path)))
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        long aggregateUncompressed = 0;
        long aggregateCompressed = 0;
        foreach (var entry in pending)
        {
            aggregateUncompressed = checked(aggregateUncompressed + entry.UncompressedLength);
            aggregateCompressed = checked(aggregateCompressed + entry.CompressedLength);
            if (entry.UncompressedLength < 0 ||
                entry.CompressedLength < 0 ||
                entry.UncompressedLength > limits.MaximumEntryUncompressedBytes ||
                ExpansionRatioExceeded(entry.UncompressedLength, entry.CompressedLength, limits.MaximumExpansionRatio))
            {
                return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
            }
        }

        if (aggregateUncompressed > limits.MaximumAggregateUncompressedBytes ||
            ExpansionRatioExceeded(aggregateUncompressed, aggregateCompressed, limits.MaximumExpansionRatio))
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        var manifestEntries = pending
            .Where(entry => entry.Path.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (manifestEntries.Length != 1 ||
            !manifestEntries[0].Path.Equals(ManifestFileName, StringComparison.Ordinal) ||
            manifestEntries[0].UncompressedLength > limits.MaximumManifestBytes)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        var entries = new List<PluginPackageEntry>(pending.Count);
        byte[]? manifestBytes = null;
        long bytesRead = 0;
        for (var index = 0; index < pending.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = pending[index];
            var capture = entry.Path.Equals(ManifestFileName, StringComparison.Ordinal);
            var read = ReadAndHashEntry(
                entry,
                capture ? limits.MaximumManifestBytes : limits.MaximumEntryUncompressedBytes,
                capture,
                cancellationToken);
            bytesRead = checked(bytesRead + read.BytesRead);
            if (bytesRead > limits.MaximumAggregateUncompressedBytes)
            {
                return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
            }

            if (capture)
            {
                manifestBytes = read.Content;
            }

            entries.Add(new PluginPackageEntry(
                entry.Path,
                read.Sha256,
                entry.IsSymlink,
                entry.UncompressedLength,
                entry.CompressedLength,
                entry.OpenRead));
            progress?.Report(new PluginPackageReadProgress(
                index + 1,
                pending.Count,
                bytesRead,
                aggregateUncompressed));
        }

        if (manifestBytes is null)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        var manifestJson = new UTF8Encoding(false, true).GetString(manifestBytes).TrimStart('\uFEFF');
        var manifest = ParseManifest(manifestJson);
        if (!manifest.IsSuccess)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        var archive = new PluginPackageArchive(
            packagePath,
            ManifestFileName,
            manifest.Manifest!,
            entries,
            manifestJson);

        return Result.Success(archive);
    }

    private static EntryReadResult ReadAndHashEntry(
        PendingEntry entry,
        long maximumBytes,
        bool captureContent,
        CancellationToken cancellationToken)
    {
        using var source = entry.OpenRead();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var content = captureContent ? new MemoryStream((int)entry.UncompressedLength) : null;
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        long total = 0;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = source.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                total = checked(total + read);
                if (total > maximumBytes || total > entry.UncompressedLength)
                {
                    throw new InvalidDataException("Plugin package entry exceeds its declared or configured bound.");
                }

                hash.AppendData(buffer, 0, read);
                content?.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        if (total != entry.UncompressedLength)
        {
            throw new InvalidDataException("Plugin package entry length differs from central-directory metadata.");
        }

        return new EntryReadResult(
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            total,
            content?.ToArray());
    }

    private static bool ExpansionRatioExceeded(long uncompressed, long compressed, int ratio)
    {
        return uncompressed > 0 && (compressed == 0 || uncompressed > checked(compressed * ratio));
    }

    private static Stream OpenArchiveEntry(string packagePath, string entryPath)
    {
        var file = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        ZipArchive? archive = null;
        try
        {
            archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);
            var entry = archive.Entries.Single(candidate => candidate.FullName.Equals(entryPath, StringComparison.Ordinal));
            return new OwnedArchiveEntryStream(entry.Open(), archive);
        }
        catch
        {
            archive?.Dispose();
            if (archive is null)
            {
                file.Dispose();
            }

            throw;
        }
    }

    private static PluginManifestValidationOutcome ParseManifest(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return PluginManifestValidationOutcome.Failure([new PluginManifestValidationReason("invalid-manifest", "Manifest root must be an object.")]);
        }

        var schemaReasons = PluginManifestSchemaValidator.Validate(root);
        if (schemaReasons.Count != 0)
        {
            return PluginManifestValidationOutcome.Failure(schemaReasons);
        }

        var allowedRootProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "pluginId",
            "version",
            "vendorId",
            "minHostVersion",
            "entryAssembly",
            "displayName",
            "description",
            "icon",
            "dependsOn",
            "capabilities",
            "operationTargets",
            "extensionPoints",
            "files",
            "signature"
        };

        foreach (var property in root.EnumerateObject())
        {
            if (!allowedRootProperties.Contains(property.Name))
            {
                return PluginManifestValidationOutcome.Failure([
                    new PluginManifestValidationReason("invalid-manifest", $"Unexpected manifest property '{property.Name}'.", property.Name)
                ]);
            }
        }

        try
        {
            var pluginId = GetString(root, "pluginId");
            var version = GetString(root, "version");
            var vendorId = GetString(root, "vendorId");
            var minHostVersion = GetString(root, "minHostVersion");
            var entryAssembly = GetString(root, "entryAssembly");
            var displayName = GetString(root, "displayName");
            var description = GetString(root, "description");
            var icon = TryGetProperty(root, "icon", out var iconElement)
                ? ParseIcon(iconElement)
                : null;
            var dependsOn = GetArray(root, "dependsOn", ParseDependency);
            var capabilities = GetArray(root, "capabilities", ParseCapability);
            var operationTargets = GetArray(root, "operationTargets", ParseOperationTarget);
            var extensionPoints = GetArray(root, "extensionPoints", ParseExtensionPoint);
            var files = GetArray(root, "files", ParseFile);
            var signature = ParseSignature(GetObject(root, "signature"));

            return PluginManifestValidationOutcome.Success(new PluginManifest(
                pluginId,
                vendorId,
                version,
                minHostVersion,
                entryAssembly,
                displayName,
                description,
                icon,
                dependsOn,
                capabilities,
                operationTargets,
                extensionPoints,
                files,
                signature));
        }
        catch (InvalidOperationException exception)
        {
            return PluginManifestValidationOutcome.Failure([
                new PluginManifestValidationReason("invalid-manifest", exception.Message)
            ]);
        }
    }

    private static PluginManifestDependency ParseDependency(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "pluginId", "minVersion");
        return new PluginManifestDependency(
            GetString(objectElement, "pluginId"),
            GetString(objectElement, "minVersion"));
    }

    private static PluginCapability ParseCapability(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "name", "scope");
        return new PluginCapability(
            GetString(objectElement, "name"),
            GetString(objectElement, "scope"));
    }

    private static PluginOperationTarget ParseOperationTarget(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "id", "verbs", "labelKey");
        return new PluginOperationTarget(
            GetString(objectElement, "id"),
            GetStringArray(objectElement, "verbs"),
            GetString(objectElement, "labelKey"));
    }

    private static PluginExtensionPoint ParseExtensionPoint(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "id", "target", "routeKey", "labelKey", "parentId", "group", "weight");
        return new PluginExtensionPoint(
            GetString(objectElement, "id"),
            GetString(objectElement, "target"),
            GetString(objectElement, "routeKey"),
            TryGetOptionalString(objectElement, "labelKey"),
            TryGetOptionalString(objectElement, "parentId"),
            TryGetOptionalString(objectElement, "group"),
            TryGetOptionalInt(objectElement, "weight"));
    }

    private static PluginPackageFile ParseFile(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "path", "sha256");
        return new PluginPackageFile(
            GetString(objectElement, "path"),
            GetString(objectElement, "sha256"));
    }

    private static PluginIcon ParseIcon(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "path");
        return new PluginIcon(GetString(objectElement, "path"));
    }

    private static PluginPackageSignature ParseSignature(JsonElement element)
    {
        EnsureOnlyProperties(element, "algorithm", "keyVersion", "value");
        return new PluginPackageSignature(
            GetString(element, "algorithm"),
            GetInt(element, "keyVersion"),
            GetString(element, "value"));
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static JsonElement GetObject(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
        }

        return GetObject(element);
    }

    private static JsonElement GetObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Manifest object value is malformed.");
        }

        return element;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
        }

        return value.GetString() ?? throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
    }

    private static string? TryGetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Manifest property '{propertyName}' must be a string when present.");
        }

        return value.GetString();
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
        }

        return value.GetInt32();
    }

    private static int? TryGetOptionalInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException($"Manifest property '{propertyName}' must be an integer when present.");
        }

        return value.GetInt32();
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
        }

        return value.EnumerateArray()
            .Select(item => item.GetString() ?? throw new InvalidOperationException($"Manifest property '{propertyName}' is required."))
            .ToArray();
    }

    private static IReadOnlyList<T> GetArray<T>(JsonElement element, string propertyName, Func<JsonElement, T> factory)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
        }

        return value.EnumerateArray().Select(factory).ToArray();
    }

    private static void EnsureOnlyProperties(JsonElement element, params string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedSet.Contains(property.Name))
            {
                throw new InvalidOperationException($"Manifest contains unexpected property '{property.Name}'.");
            }
        }
    }

    private static bool IsSymlink(ZipArchiveEntry entry)
    {
        var externalAttributes = entry.ExternalAttributes >> 16;
        var fileType = externalAttributes & 0xF000;
        return fileType == 0xA000;
    }

    private sealed record PendingEntry(
        string Path,
        long UncompressedLength,
        long CompressedLength,
        bool IsSymlink,
        Func<Stream> OpenRead);

    private sealed record EntryReadResult(
        string Sha256,
        long BytesRead,
        byte[]? Content);

    private sealed class OwnedArchiveEntryStream : Stream
    {
        private readonly Stream inner;
        private readonly ZipArchive owner;
        private bool disposed;

        public OwnedArchiveEntryStream(Stream inner, ZipArchive owner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                try
                {
                    inner.Dispose();
                }
                finally
                {
                    owner.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Dobby.Core;
using Dobby.PluginFramework.Packaging;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework.Packaging;

public sealed class PluginPackageReaderTests
{
    [Fact]
    public void Read_directory_package_parses_manifest_and_entries()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "content"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "bin", "plugins"));
        File.WriteAllText(
            Path.Combine(temp.Path, PluginPackageReader.ManifestFileName),
            PluginPackageTestData.CreateManifestJson(),
            Encoding.UTF8);
        File.WriteAllBytes(
            Path.Combine(temp.Path, PluginPackageTestData.EntryAssembly.Replace('/', Path.DirectorySeparatorChar)),
            PluginPackageTestData.EntryAssemblyContent);
        File.WriteAllBytes(Path.Combine(temp.Path, "content", "hello.txt"), PluginPackageTestData.FileContent);

        var result = PluginPackageReader.Read(temp.Path);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Manifest.PluginId.ShouldBe(PluginPackageTestData.PluginId);
        result.Value.Manifest.Files.Select(file => file.Path).ToArray().ShouldBe([
            PluginPackageTestData.EntryAssembly,
            PluginPackageTestData.FilePath
        ]);
        result.Value.Entries.Select(entry => entry.Path).ShouldContain(PluginPackageTestData.EntryAssembly);
        result.Value.Entries.Select(entry => entry.Path).ShouldContain(PluginPackageTestData.FilePath);
        result.Value.Entries.Select(entry => entry.Path).ShouldContain(PluginPackageReader.ManifestFileName);
    }

    [Fact]
    public void Read_zip_package_parses_manifest_and_entries()
    {
        using var temp = new TempDirectory();
        var zipPath = Path.Combine(temp.Path, "package.dobbyplugin");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry(PluginPackageReader.ManifestFileName);
            using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
            {
                writer.Write(PluginPackageTestData.CreateManifestJson());
            }

            var assemblyEntry = archive.CreateEntry(PluginPackageTestData.EntryAssembly);
            using (var assemblyStream = assemblyEntry.Open())
            {
                assemblyStream.Write(PluginPackageTestData.EntryAssemblyContent);
            }

            var fileEntry = archive.CreateEntry(PluginPackageTestData.FilePath);
            using var fileStream = fileEntry.Open();
            fileStream.Write(PluginPackageTestData.FileContent);
        }

        var result = PluginPackageReader.Read(zipPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Manifest.DisplayName.ShouldBe(PluginPackageTestData.DisplayName);
        result.Value.Entries.Select(entry => entry.Path).ShouldContain(PluginPackageTestData.FilePath);
    }

    [Fact]
    public void Read_zip_keeps_payload_stream_backed_instead_of_retaining_it_in_memory()
    {
        using var temp = new TempDirectory();
        var zipPath = CreateZip(temp.Path,
            (PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson())),
            (PluginPackageTestData.FilePath, PluginPackageTestData.FileContent));

        var result = PluginPackageReader.Read(zipPath);

        result.IsSuccess.ShouldBeTrue();
        var payload = result.Value.Entries.Single(entry => entry.Path == PluginPackageTestData.FilePath);
        payload.Content.ShouldBeEmpty();
        using var stream = payload.OpenRead();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.ToArray().ShouldBe(PluginPackageTestData.FileContent);
    }

    [Fact]
    public void Read_enforces_manifest_limit_at_the_exact_byte_boundary()
    {
        using var temp = new TempDirectory();
        var manifest = Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson());
        var zipPath = CreateZip(temp.Path, (PluginPackageReader.ManifestFileName, manifest));

        PluginPackageReader.Read(zipPath, Limits(manifestBytes: manifest.Length)).IsSuccess.ShouldBeTrue();
        PluginPackageReader.Read(zipPath, Limits(manifestBytes: manifest.Length - 1)).ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Fact]
    public void Read_enforces_entry_count_at_the_exact_boundary()
    {
        using var temp = new TempDirectory();
        var zipPath = CreateZip(temp.Path,
            (PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson())),
            (PluginPackageTestData.FilePath, PluginPackageTestData.FileContent));

        PluginPackageReader.Read(zipPath, Limits(entryCount: 2)).IsSuccess.ShouldBeTrue();
        PluginPackageReader.Read(zipPath, Limits(entryCount: 1)).ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Fact]
    public void Read_enforces_per_entry_and_aggregate_limits_at_exact_boundaries()
    {
        using var temp = new TempDirectory();
        var manifest = Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson());
        var payload = new byte[2048];
        new Random(1234).NextBytes(payload);
        var zipPath = CreateZip(temp.Path,
            (PluginPackageReader.ManifestFileName, manifest),
            (PluginPackageTestData.FilePath, payload));
        var aggregate = manifest.LongLength + payload.LongLength;

        PluginPackageReader.Read(zipPath, Limits(perEntryBytes: payload.Length, aggregateBytes: aggregate)).IsSuccess.ShouldBeTrue();
        PluginPackageReader.Read(zipPath, Limits(perEntryBytes: payload.Length - 1, aggregateBytes: aggregate)).ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        PluginPackageReader.Read(zipPath, Limits(perEntryBytes: payload.Length, aggregateBytes: aggregate - 1)).ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Fact]
    public void Read_enforces_compressed_package_limit_at_the_exact_boundary()
    {
        using var temp = new TempDirectory();
        var zipPath = CreateZip(temp.Path,
            (PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson())));
        var compressedBytes = new FileInfo(zipPath).Length;

        PluginPackageReader.Read(zipPath, Limits(compressedBytes: compressedBytes)).IsSuccess.ShouldBeTrue();
        PluginPackageReader.Read(zipPath, Limits(compressedBytes: compressedBytes - 1)).ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Fact]
    public void Read_rejects_high_expansion_ratio_from_central_directory_metadata()
    {
        using var temp = new TempDirectory();
        var zipPath = CreateZip(temp.Path,
            (PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson())),
            (PluginPackageTestData.FilePath, new byte[64 * 1024]));

        var result = PluginPackageReader.Read(zipPath, Limits(perEntryBytes: 128 * 1024, aggregateBytes: 256 * 1024, ratio: 10));

        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("/rooted.txt")]
    [InlineData("C:/drive.txt")]
    [InlineData("folder\\backslash.txt")]
    [InlineData("folder//empty.txt")]
    [InlineData("folder/./dot.txt")]
    [InlineData("folder/../parent.txt")]
    [InlineData("folder/trailing-dot.")]
    [InlineData("folder/trailing-space ")]
    [InlineData("CON.txt")]
    [InlineData("folder/aux.json")]
    [InlineData("folder/ads:name.txt")]
    public void Read_rejects_hostile_archive_paths(string hostilePath)
    {
        using var temp = new TempDirectory();
        var zipPath = CreateZip(temp.Path,
            (PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson())),
            (hostilePath, [1]));

        PluginPackageReader.Read(zipPath).ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Theory]
    [InlineData("content/File.txt", "content/file.txt")]
    [InlineData("content/caf\u00e9.txt", "content/cafe\u0301.txt")]
    [InlineData("content/payload", "content/payload/child.txt")]
    public void Read_rejects_case_unicode_and_file_directory_prefix_collisions(string first, string second)
    {
        using var temp = new TempDirectory();
        var zipPath = CreateZip(temp.Path,
            (PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson())),
            (first, [1]),
            (second, [2]));

        PluginPackageReader.Read(zipPath).ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Fact]
    public void Read_honors_pre_cancelled_ingestion_without_reporting_progress()
    {
        using var temp = new TempDirectory();
        var zipPath = CreateZip(temp.Path,
            (PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson())));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var progress = new ImmediateProgress();

        Should.Throw<OperationCanceledException>(() =>
            PluginPackageReader.Read(zipPath, cancellationToken: cancellation.Token, progress: progress));
        progress.Values.ShouldBeEmpty();
    }

    [Fact]
    public void Read_reports_deterministic_monotonic_entry_and_byte_progress()
    {
        using var temp = new TempDirectory();
        var manifest = Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson());
        var zipPath = CreateZip(temp.Path,
            (PluginPackageReader.ManifestFileName, manifest),
            (PluginPackageTestData.FilePath, PluginPackageTestData.FileContent));
        var progress = new ImmediateProgress();

        PluginPackageReader.Read(zipPath, progress: progress).IsSuccess.ShouldBeTrue();

        progress.Values.Count.ShouldBe(2);
        progress.Values.Select(value => value.EntriesCompleted).ShouldBe([1, 2]);
        progress.Values.Select(value => value.UncompressedBytesRead).ShouldBeInOrder(SortDirection.Ascending);
        progress.Values[^1].UncompressedBytesRead.ShouldBe(manifest.LongLength + PluginPackageTestData.FileContent.LongLength);
        progress.Values[^1].TotalUncompressedBytes.ShouldBe(progress.Values[^1].UncompressedBytesRead);
    }

    [Fact]
    public void Read_valid_manifests_concurrently_through_embedded_schema()
    {
        var failures = new ConcurrentBag<string>();

        Parallel.For(0, 64, index =>
        {
            using var temp = new TempDirectory();
            Directory.CreateDirectory(Path.Combine(temp.Path, "content"));
            Directory.CreateDirectory(Path.Combine(temp.Path, "bin", "plugins"));
            File.WriteAllText(
                Path.Combine(temp.Path, PluginPackageReader.ManifestFileName),
                PluginPackageTestData.CreateManifestJson(),
                Encoding.UTF8);
            File.WriteAllBytes(
                Path.Combine(temp.Path, PluginPackageTestData.EntryAssembly.Replace('/', Path.DirectorySeparatorChar)),
                PluginPackageTestData.EntryAssemblyContent);
            File.WriteAllBytes(Path.Combine(temp.Path, "content", "hello.txt"), PluginPackageTestData.FileContent);

            var result = PluginPackageReader.Read(temp.Path);
            if (!result.IsSuccess)
            {
                failures.Add($"{index}: {result.ErrorCode}");
            }
        });

        failures.ShouldBeEmpty();
    }

    [Fact]
    public void Read_rejects_manifest_with_unexpected_root_property()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            Path.Combine(temp.Path, PluginPackageReader.ManifestFileName),
            PluginPackageTestData.CreateManifestJson(includeUnexpectedRootProperty: true),
            Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(temp.Path, "content.txt"), PluginPackageTestData.FileContent);

        var result = PluginPackageReader.Read(temp.Path);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Read_rejects_manifest_declaring_a_non_v2_schema_version(int schemaVersion)
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "content"));
        File.WriteAllText(
            Path.Combine(temp.Path, PluginPackageReader.ManifestFileName),
            CreateManifestJsonWithRootOverride("schemaVersion", schemaVersion),
            Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(temp.Path, "content", "hello.txt"), PluginPackageTestData.FileContent);

        var result = PluginPackageReader.Read(temp.Path);

        result.IsSuccess.ShouldBeFalse(
            "docs/schemas/artifact-compatibility-matrix.md declares v2 the only accepted manifest " +
            "version, so the reader must refuse a legacy or unknown version before typed projection");
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Fact]
    public void Read_rejects_manifest_that_fails_embedded_schema_before_typed_projection()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "content"));
        File.WriteAllText(
            Path.Combine(temp.Path, PluginPackageReader.ManifestFileName),
            CreateManifestJsonWithRootOverride("displayName", string.Empty),
            Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(temp.Path, "content", "hello.txt"), PluginPackageTestData.FileContent);

        var result = PluginPackageReader.Read(temp.Path);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Theory]
    [InlineData("group", 7)]
    [InlineData("weight", "heavy")]
    [InlineData("labelKey", 42)]
    [InlineData("parentId", false)]
    public void Read_rejects_present_optional_extension_fields_with_wrong_types(string propertyName, object value)
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "content"));
        File.WriteAllText(
            Path.Combine(temp.Path, PluginPackageReader.ManifestFileName),
            CreateManifestJsonWithExtensionOverride(propertyName, value),
            Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(temp.Path, "content", "hello.txt"), PluginPackageTestData.FileContent);

        var result = PluginPackageReader.Read(temp.Path);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    private static string CreateManifestJsonWithExtensionOverride(string propertyName, object value)
    {
        var root = JsonNode.Parse(PluginPackageTestData.CreateManifestJson())!.AsObject();
        var extension = root["extensionPoints"]!.AsArray()[0]!.AsObject();
        extension[propertyName] = JsonValue.Create(value);
        return root.ToJsonString();
    }

    private static string CreateManifestJsonWithRootOverride(string propertyName, object value)
    {
        var root = JsonNode.Parse(PluginPackageTestData.CreateManifestJson())!.AsObject();
        root[propertyName] = JsonValue.Create(value);
        return root.ToJsonString();
    }

    [Theory]
    [InlineData("\"keyId\":\"plugin-prod-2026-01\",\"keyVersion\":1", "adds the retired numeric selector")]
    [InlineData("\"keyVersion\":1", "substitutes the retired numeric selector for keyId")]
    public void Read_rejects_a_signature_block_carrying_the_retired_key_version_selector(
        string replacement, string reason)
    {
        using var temp = new TempDirectory();
        var manifestJson = PluginPackageTestData.CreateManifestJson();

        // Baseline: the untouched manifest reads successfully, so the reject below is
        // attributable to the retired selector and not to any other manifest defect.
        var baselinePath = CreateZip(
            temp.Path,
            (PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson)),
            (PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyContent),
            (PluginPackageTestData.FilePath, PluginPackageTestData.FileContent));
        PluginPackageReader.Read(baselinePath).IsSuccess.ShouldBeTrue();

        var mutatedJson = manifestJson.Replace(
            "\"keyId\":\"plugin-prod-2026-01\"",
            replacement,
            StringComparison.Ordinal);
        mutatedJson.ShouldNotBe(manifestJson, "the fixture must actually contain the canonical keyId");

        var mutatedPath = CreateZip(
            temp.Path,
            (PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(mutatedJson)),
            (PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyContent),
            (PluginPackageTestData.FilePath, PluginPackageTestData.FileContent));

        // ADR-0062 § 1 retires numeric keyVersion; ADR-0045 pins the signature block to
        // exactly purpose/algorithm/format/keyId/value, so the selector can never reach
        // key resolution regardless of how well-formed the rest of the package is.
        PluginPackageReader.Read(mutatedPath).ErrorCode.ShouldBe(
            ErrorCode.InvalidInput,
            $"a manifest signature block that {reason} must be rejected (ADR-0062 § 1, ADR-0045)");
    }

    private static string CreateZip(string root, params (string Path, byte[] Content)[] entries)
    {
        var zipPath = Path.Combine(root, $"{Guid.NewGuid():N}.dobbyplugin");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (path, content) in entries)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(content);
        }

        return zipPath;
    }

    private static PluginPackageLimits Limits(
        long compressedBytes = 1024 * 1024,
        int manifestBytes = 128 * 1024,
        int entryCount = 16,
        long perEntryBytes = 128 * 1024,
        long aggregateBytes = 256 * 1024,
        int ratio = 200)
    {
        return new PluginPackageLimits(
            compressedBytes,
            manifestBytes,
            entryCount,
            perEntryBytes,
            Math.Max(aggregateBytes, perEntryBytes),
            ratio);
    }

    private sealed class ImmediateProgress : IProgress<PluginPackageReadProgress>
    {
        public List<PluginPackageReadProgress> Values { get; } = [];

        public void Report(PluginPackageReadProgress value) => Values.Add(value);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

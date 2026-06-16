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
        File.WriteAllText(
            Path.Combine(temp.Path, PluginPackageReader.ManifestFileName),
            PluginPackageTestData.CreateManifestJson(),
            Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(temp.Path, "content", "hello.txt"), PluginPackageTestData.FileContent);

        var result = PluginPackageReader.Read(temp.Path);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Manifest.PluginId.ShouldBe(PluginPackageTestData.PluginId);
        result.Value.Manifest.Files.Single().Path.ShouldBe(PluginPackageTestData.FilePath);
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
    public void Read_valid_manifests_concurrently_through_embedded_schema()
    {
        var failures = new ConcurrentBag<string>();

        Parallel.For(0, 64, index =>
        {
            using var temp = new TempDirectory();
            Directory.CreateDirectory(Path.Combine(temp.Path, "content"));
            File.WriteAllText(
                Path.Combine(temp.Path, PluginPackageReader.ManifestFileName),
                PluginPackageTestData.CreateManifestJson(),
                Encoding.UTF8);
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

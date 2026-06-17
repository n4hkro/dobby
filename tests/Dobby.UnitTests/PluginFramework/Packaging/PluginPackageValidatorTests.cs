using System.Text;
using Dobby.Core.Versioning;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework.Packaging;

public sealed class PluginPackageValidatorTests
{
    [Fact]
    public void Validate_accepts_canonical_package_when_trust_verification_succeeds()
    {
        var manifest = PluginPackageTestData.CreateManifest();
        var archive = CreateArchive(manifest, [
            new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson()), PluginPackageTestData.HashHex("manifest"), false),
            new PluginPackageEntry(PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyContent, PluginPackageTestData.EntryAssemblyHash, false),
            new PluginPackageEntry(PluginPackageTestData.FilePath, PluginPackageTestData.FileContent, PluginPackageTestData.FileHash, false)
        ]);

        var result = PluginPackageValidator.Validate(archive, new HostVersion(1, 0, 0), new AllowAllTrustVerifier());

        result.IsSuccess.ShouldBeTrue();
        result.Descriptor.ShouldNotBeNull();
    }

    [Fact]
    public void Validate_requires_trust_verifier()
    {
        var manifest = PluginPackageTestData.CreateManifest();
        var archive = CreateArchive(manifest, [
            new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson()), PluginPackageTestData.HashHex("manifest"), false),
            new PluginPackageEntry(PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyContent, PluginPackageTestData.EntryAssemblyHash, false),
            new PluginPackageEntry(PluginPackageTestData.FilePath, PluginPackageTestData.FileContent, PluginPackageTestData.FileHash, false)
        ]);

        Should.Throw<ArgumentNullException>(() =>
            PluginPackageValidator.Validate(archive, new HostVersion(1, 0, 0), null!));
    }

    [Fact]
    public void Validate_rejects_hash_mismatch()
    {
        var manifest = PluginPackageTestData.CreateManifest();
        var archive = CreateArchive(manifest, [
            new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson()), PluginPackageTestData.HashHex("manifest"), false),
            new PluginPackageEntry(PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyContent, PluginPackageTestData.EntryAssemblyHash, false),
            new PluginPackageEntry(PluginPackageTestData.FilePath, Encoding.UTF8.GetBytes("tampered"), PluginPackageTestData.HashHex("tampered"), false)
        ]);

        var result = PluginPackageValidator.Validate(archive, new HostVersion(1, 0, 0), new AllowAllTrustVerifier());

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Single().Code.ShouldBe("hash-mismatch");
        result.Reasons.Single().Path.ShouldBe(PluginPackageTestData.FilePath);
    }

    [Fact]
    public void Validate_rejects_manifest_when_entry_assembly_is_not_declared_in_files()
    {
        var manifest = PluginPackageTestData.CreateManifest(
            files: [new PluginPackageFile(PluginPackageTestData.FilePath, PluginPackageTestData.FileHash)]);
        var archive = CreateArchive(manifest, [
            new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson()), PluginPackageTestData.HashHex("manifest"), false),
            new PluginPackageEntry(PluginPackageTestData.FilePath, PluginPackageTestData.FileContent, PluginPackageTestData.FileHash, false)
        ]);

        var result = PluginPackageValidator.Validate(archive, new HostVersion(1, 0, 0), new AllowAllTrustVerifier());

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.ShouldContain(reason =>
            reason.Code == "invalid-manifest" &&
            reason.Path == "entryAssembly");
    }

    [Fact]
    public void Validate_rejects_case_colliding_entry()
    {
        var manifest = PluginPackageTestData.CreateManifest(
            files:
            [
                new PluginPackageFile(PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyHash),
                new PluginPackageFile(PluginPackageTestData.FilePath, PluginPackageTestData.FileHash)
            ]);
        var archive = CreateArchive(manifest, [
            new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson()), PluginPackageTestData.HashHex("manifest"), false),
            new PluginPackageEntry(PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyContent, PluginPackageTestData.EntryAssemblyHash, false),
            new PluginPackageEntry(PluginPackageTestData.AlternateFilePath, PluginPackageTestData.FileContent, PluginPackageTestData.FileHash, false)
        ]);

        var result = PluginPackageValidator.Validate(archive, new HostVersion(1, 0, 0), new AllowAllTrustVerifier());

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Count.ShouldBe(2);
        result.Reasons.All(reason => reason.Code == "forbidden-content").ShouldBeTrue();
    }

    [Fact]
    public void Validate_rejects_symlink_entry()
    {
        var manifest = PluginPackageTestData.CreateManifest();
        var archive = CreateArchive(manifest, [
            new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson()), PluginPackageTestData.HashHex("manifest"), false),
            new PluginPackageEntry(PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyContent, PluginPackageTestData.EntryAssemblyHash, false),
            new PluginPackageEntry(PluginPackageTestData.FilePath, PluginPackageTestData.FileContent, PluginPackageTestData.FileHash, true)
        ]);

        var result = PluginPackageValidator.Validate(archive, new HostVersion(1, 0, 0), new AllowAllTrustVerifier());

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Single().Code.ShouldBe("forbidden-content");
    }

    [Fact]
    public void Validate_propagates_trust_failure()
    {
        var manifest = PluginPackageTestData.CreateManifest();
        var archive = CreateArchive(manifest, [
            new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson()), PluginPackageTestData.HashHex("manifest"), false),
            new PluginPackageEntry(PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyContent, PluginPackageTestData.EntryAssemblyHash, false),
            new PluginPackageEntry(PluginPackageTestData.FilePath, PluginPackageTestData.FileContent, PluginPackageTestData.FileHash, false)
        ]);

        var result = PluginPackageValidator.Validate(archive, new HostVersion(1, 0, 0), new RejectingTrustVerifier("trust-failed", "not trusted"));

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Single().Code.ShouldBe("trust-failed");
        result.Reasons.Single().Path.ShouldBe("signature");
    }

    [Theory]
    [InlineData("licenses/customer.dobbylicense")]
    [InlineData("customer-data/export.json")]
    [InlineData("device-fingerprint/device.json")]
    [InlineData("keys/vendor-private-key.pem")]
    [InlineData("config/host.internal.config.json")]
    [InlineData("persistence/migrations/0003_customer.sql")]
    public void Validate_rejects_forbidden_semantic_package_content(string forbiddenPath)
    {
        var content = Encoding.UTF8.GetBytes("forbidden");
        var hash = PluginPackageTestData.HashHex(content);
        var manifest = PluginPackageTestData.CreateManifest(
            files:
            [
                new PluginPackageFile(PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyHash),
                new PluginPackageFile(forbiddenPath, hash)
            ]);
        var archive = CreateArchive(manifest, [
            new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson()), PluginPackageTestData.HashHex("manifest"), false),
            new PluginPackageEntry(PluginPackageTestData.EntryAssembly, PluginPackageTestData.EntryAssemblyContent, PluginPackageTestData.EntryAssemblyHash, false),
            new PluginPackageEntry(forbiddenPath, content, hash, false)
        ]);

        var result = PluginPackageValidator.Validate(archive, new HostVersion(1, 0, 0), new AllowAllTrustVerifier());

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Any(reason => reason.Code == "forbidden-content" && reason.Path == forbiddenPath).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Dobby.Infrastructure")]
    [InlineData("Dobby.Host")]
    [InlineData("System.Reflection.Assembly.Load")]
    [InlineData("Assembly.Load")]
    [InlineData("AssemblyLoadContext")]
    public void Validate_rejects_plugin_assembly_host_boundary_and_dynamic_load_markers(string marker)
    {
        var assemblyPath = "bin/plugins/com.dobby.sample.dll";
        var content = Encoding.UTF8.GetBytes($"plugin bytes {marker}");
        var hash = PluginPackageTestData.HashHex(content);
        var manifest = PluginPackageTestData.CreateManifest(
            entryAssembly: assemblyPath,
            files: [new PluginPackageFile(assemblyPath, hash)]);
        var archive = CreateArchive(manifest, [
            new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(PluginPackageTestData.CreateManifestJson()), PluginPackageTestData.HashHex("manifest"), false),
            new PluginPackageEntry(assemblyPath, content, hash, false)
        ]);

        var result = PluginPackageValidator.Validate(archive, new HostVersion(1, 0, 0), new AllowAllTrustVerifier());

        result.IsSuccess.ShouldBeFalse();
        result.Reasons.Any(reason => reason.Code == "forbidden-content" && reason.Path == assemblyPath).ShouldBeTrue();
    }

    private static PluginPackageArchive CreateArchive(PluginManifest manifest, IReadOnlyList<PluginPackageEntry> entries)
    {
        return new PluginPackageArchive(
            "package.dobbyplugin",
            PluginPackageReader.ManifestFileName,
            manifest,
            entries,
            PluginPackageTestData.CreateManifestJson());
    }

    private sealed class AllowAllTrustVerifier : IPluginTrustVerifier
    {
        public PluginTrustVerificationResult Verify(PluginTrustPayload payload) => PluginTrustVerificationResult.Success();
    }

    private sealed class RejectingTrustVerifier(string code, string message) : IPluginTrustVerifier
    {
        public PluginTrustVerificationResult Verify(PluginTrustPayload payload) => PluginTrustVerificationResult.Failure(code, message);
    }
}

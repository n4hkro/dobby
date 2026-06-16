using System.IO.Compression;
using System.Text;
using Dobby.Audit;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Host.Plugins;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Trust;
using Dobby.UnitTests.PluginFramework.Packaging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class HostPluginPackageInstallCoordinatorTests
{
    [Fact]
    public void Constructor_requires_trust_verifier()
    {
        Should.Throw<ArgumentNullException>(() =>
            new HostPluginPackageInstallCoordinator(
                CreateSuccessfulStateStore(),
                CreateSuccessfulMetadataCache(),
                Substitute.For<IPluginInstallStagingArea>(),
                CreateAuditCoordinator(new InMemoryAuditTrail()),
                null!));
    }

    [Fact]
    public void Install_package_reads_validates_installs_and_audits_verified_package()
    {
        var packagePath = CreatePackageZip(
            PluginPackageTestData.CreateManifestJson(),
            PluginPackageTestData.FilePath,
            PluginPackageTestData.FileContent);
        try
        {
            var audit = new InMemoryAuditTrail();
            var staging = CreateSuccessfulStaging();
            var stateStore = CreateSuccessfulStateStore();
            var metadataCache = CreateSuccessfulMetadataCache();
            var coordinator = new HostPluginPackageInstallCoordinator(
                stateStore,
                metadataCache,
                staging,
                CreateAuditCoordinator(audit),
                new AllowAllTrustVerifier());

            var result = coordinator.InstallPackage(
                "owner",
                packagePath,
                new HostVersion(1, 0, 0),
                CorrelationId.New());

            result.IsSuccess.ShouldBeTrue();
            result.Value.PluginId.ShouldBe(PluginPackageTestData.PluginId);
            staging.Received(1).Stage(Arg.Any<PluginPackageDescriptor>());
            staging.Received(1).Promote("stage", Arg.Any<PluginPackageDescriptor>());
            stateStore.Received(1).Upsert(Arg.Any<InstalledPluginState>());
            metadataCache.Received(1).Upsert(Arg.Any<InstalledPluginMetadata>());
            audit.Snapshot().Select(evt => evt.EventName).ShouldBe([
                AuditEventName.PluginInstallAttempted,
                AuditEventName.PluginInstallVerified
            ]);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    [Fact]
    public void Install_package_rejects_forbidden_content_before_staging_and_audits_rejection()
    {
        var forbiddenPath = "customer-data/export.json";
        var forbiddenBytes = Encoding.UTF8.GetBytes("customer payload");
        var manifestJson = PluginPackageTestData.CreateManifestJson(
            files: [new PluginPackageFile(forbiddenPath, PluginPackageTestData.HashHex(forbiddenBytes))]);
        var packagePath = CreatePackageZip(manifestJson, forbiddenPath, forbiddenBytes);
        try
        {
            var audit = new InMemoryAuditTrail();
            var staging = CreateSuccessfulStaging();
            var coordinator = new HostPluginPackageInstallCoordinator(
                CreateSuccessfulStateStore(),
                CreateSuccessfulMetadataCache(),
                staging,
                CreateAuditCoordinator(audit),
                new AllowAllTrustVerifier());

            var result = coordinator.InstallPackage(
                "owner",
                packagePath,
                new HostVersion(1, 0, 0),
                CorrelationId.New());

            result.IsSuccess.ShouldBeFalse();
            result.ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
            staging.DidNotReceive().Stage(Arg.Any<PluginPackageDescriptor>());
            audit.Snapshot().Select(evt => evt.EventName).ShouldBe([
                AuditEventName.PluginInstallAttempted,
                AuditEventName.PluginInstallRejected
            ]);
            audit.Snapshot().Last().Details!["reasonCode"].ShouldBe("forbidden-content");
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    [Fact]
    public void Install_package_records_diagnostics_for_validation_rejection()
    {
        var forbiddenPath = "customer-data/export.json";
        var forbiddenBytes = Encoding.UTF8.GetBytes("customer payload");
        var manifestJson = PluginPackageTestData.CreateManifestJson(
            files: [new PluginPackageFile(forbiddenPath, PluginPackageTestData.HashHex(forbiddenBytes))]);
        var packagePath = CreatePackageZip(manifestJson, forbiddenPath, forbiddenBytes);
        try
        {
            var diagnostics = new List<PluginRuntimeDiagnosticEntry>();
            var staging = CreateSuccessfulStaging();
            var coordinator = new HostPluginPackageInstallCoordinator(
                CreateSuccessfulStateStore(),
                CreateSuccessfulMetadataCache(),
                staging,
                CreateAuditCoordinator(new InMemoryAuditTrail()),
                new AllowAllTrustVerifier(),
                new PluginRuntimeDiagnosticsCoordinator(diagnostics.Add));

            var result = coordinator.InstallPackage(
                "owner",
                packagePath,
                new HostVersion(1, 0, 0),
                CorrelationId.New());

            result.IsSuccess.ShouldBeFalse();
            staging.DidNotReceive().Stage(Arg.Any<PluginPackageDescriptor>());
            var entry = diagnostics.Single();
            entry.PluginId.ShouldBe(PluginPackageTestData.PluginId);
            entry.Marker.ShouldBe("plugin.install.rejected:forbidden-content");
            entry.Details.ShouldContain(forbiddenPath);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    [Fact]
    public void Install_package_maps_trust_failure_to_trust_audit_event_before_staging()
    {
        var packagePath = CreatePackageZip(
            PluginPackageTestData.CreateManifestJson(),
            PluginPackageTestData.FilePath,
            PluginPackageTestData.FileContent);
        try
        {
            var audit = new InMemoryAuditTrail();
            var staging = CreateSuccessfulStaging();
            var coordinator = new HostPluginPackageInstallCoordinator(
                CreateSuccessfulStateStore(),
                CreateSuccessfulMetadataCache(),
                staging,
                CreateAuditCoordinator(audit),
                new RejectingTrustVerifier("trust-failed", "not trusted"));

            var result = coordinator.InstallPackage(
                "owner",
                packagePath,
                new HostVersion(1, 0, 0),
                CorrelationId.New());

            result.IsSuccess.ShouldBeFalse();
            staging.DidNotReceive().Stage(Arg.Any<PluginPackageDescriptor>());
            audit.Snapshot().Select(evt => evt.EventName).ShouldBe([
                AuditEventName.PluginInstallAttempted,
                AuditEventName.PluginInstallTrustFailed
            ]);
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    [Fact]
    public void Install_package_rejects_directory_source_before_staging()
    {
        var packagePath = CreatePackageDirectory(
            PluginPackageTestData.CreateManifestJson(),
            PluginPackageTestData.FilePath,
            PluginPackageTestData.FileContent);
        try
        {
            var audit = new InMemoryAuditTrail();
            var staging = CreateSuccessfulStaging();
            var coordinator = new HostPluginPackageInstallCoordinator(
                CreateSuccessfulStateStore(),
                CreateSuccessfulMetadataCache(),
                staging,
                CreateAuditCoordinator(audit),
                new AllowAllTrustVerifier());

            var result = coordinator.InstallPackage(
                "owner",
                packagePath,
                new HostVersion(1, 0, 0),
                CorrelationId.New());

            result.IsSuccess.ShouldBeFalse();
            result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
            staging.DidNotReceive().Stage(Arg.Any<PluginPackageDescriptor>());
            audit.Snapshot().Select(evt => evt.EventName).ShouldBe([
                AuditEventName.PluginInstallAttempted,
                AuditEventName.PluginInstallRejected
            ]);
            audit.Snapshot().Last().Details!["reasonCode"].ShouldBe("unsupported-package-source");
        }
        finally
        {
            DeletePackage(packagePath);
        }
    }

    private static PluginRuntimeAuditCoordinator CreateAuditCoordinator(InMemoryAuditTrail audit)
    {
        return new PluginRuntimeAuditCoordinator(
            audit,
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-09T00:00:00Z")));
    }

    private static IPluginInstallStagingArea CreateSuccessfulStaging()
    {
        var staging = Substitute.For<IPluginInstallStagingArea>();
        staging.Stage(Arg.Any<PluginPackageDescriptor>()).Returns(Result.Success("stage"));
        staging.Promote("stage", Arg.Any<PluginPackageDescriptor>()).Returns(Result.Success("runtime"));
        staging.Cleanup(Arg.Any<string>()).Returns(Result.Success());
        return staging;
    }

    private static IInstalledPluginStateStore CreateSuccessfulStateStore()
    {
        var store = Substitute.For<IInstalledPluginStateStore>();
        store.Upsert(Arg.Any<InstalledPluginState>()).Returns(Result.Success());
        store.Remove(Arg.Any<string>()).Returns(Result.Success());
        return store;
    }

    private static IInstalledPluginMetadataCache CreateSuccessfulMetadataCache()
    {
        var cache = Substitute.For<IInstalledPluginMetadataCache>();
        cache.Upsert(Arg.Any<InstalledPluginMetadata>()).Returns(Result.Success());
        cache.Remove(Arg.Any<string>()).Returns(Result.Success());
        return cache;
    }

    private static string CreatePackageDirectory(string manifestJson, string filePath, byte[] content)
    {
        var packagePath = Path.Combine(Path.GetTempPath(), "dobby-package-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packagePath);
        File.WriteAllText(Path.Combine(packagePath, PluginPackageReader.ManifestFileName), manifestJson, Encoding.UTF8);
        var fullPath = Path.Combine(packagePath, filePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        return packagePath;
    }

    private static string CreatePackageZip(string manifestJson, string filePath, byte[] content)
    {
        var packagePath = Path.Combine(Path.GetTempPath(), "dobby-package-install-" + Guid.NewGuid().ToString("N") + ".dobbyplugin");
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        WriteZipEntry(archive, PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson));
        WriteZipEntry(archive, filePath, content);
        return packagePath;
    }

    private static void WriteZipEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static void DeletePackage(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
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

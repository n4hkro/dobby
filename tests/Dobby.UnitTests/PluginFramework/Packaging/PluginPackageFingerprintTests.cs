using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework.Packaging;

public sealed class PluginPackageFingerprintTests
{
    [Fact]
    public void Compute_ignores_file_order_in_manifest()
    {
        var manifestA = PluginPackageTestData.CreateManifest(
            files:
            [
                new PluginPackageFile("b.txt", PluginPackageTestData.HashHex("b")),
                new PluginPackageFile("a.txt", PluginPackageTestData.HashHex("a"))
            ]);

        var manifestB = PluginPackageTestData.CreateManifest(
            files:
            [
                new PluginPackageFile("a.txt", PluginPackageTestData.HashHex("a")),
                new PluginPackageFile("b.txt", PluginPackageTestData.HashHex("b"))
            ]);

        var first = PluginPackageFingerprint.Compute(manifestA);
        var second = PluginPackageFingerprint.Compute(manifestB);

        first.Fingerprint.ShouldBe(second.Fingerprint);
        first.CanonicalPayload.ShouldBe(second.CanonicalPayload);
    }

    [Fact]
    public void Compute_ignores_signature_value()
    {
        var manifestA = PluginPackageTestData.CreateManifest(signature: new PluginPackageSignature("ECDSA-P256-SHA256", 1, "AQ"));
        var manifestB = PluginPackageTestData.CreateManifest(signature: new PluginPackageSignature("ECDSA-P256-SHA256", 1, "Ag"));

        var first = PluginPackageFingerprint.Compute(manifestA);
        var second = PluginPackageFingerprint.Compute(manifestB);

        first.Fingerprint.ShouldBe(second.Fingerprint);
        first.CanonicalPayload.ShouldBe(second.CanonicalPayload);
    }
}

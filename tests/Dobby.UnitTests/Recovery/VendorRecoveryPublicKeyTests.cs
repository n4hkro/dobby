using Dobby.Security.Authentication;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class VendorRecoveryPublicKeyTests
{
    [Fact]
    public void Production_ring_rejects_malformed_unknown_and_cross_purpose_material()
    {
        IVendorRecoveryPublicKey publicKey = new VendorRecoveryPublicKey();
        var now = DateTimeOffset.Parse("2026-08-21T00:00:00Z");

        publicKey.Verify(
            VendorRecoveryPublicKey.ProductionKeyId,
            ArtifactSignaturePurposes.OwnerRecoveryResponse,
            [1, 2, 3],
            [4, 5, 6],
            now).ShouldBeFalse();
        publicKey.Verify(
            "unknown-key",
            ArtifactSignaturePurposes.OwnerRecoveryResponse,
            [1, 2, 3],
            new byte[64],
            now).ShouldBeFalse();
        publicKey.Verify(
            VendorRecoveryPublicKey.ProductionKeyId,
            ArtifactSignaturePurposes.PluginPackage,
            [1, 2, 3],
            new byte[64],
            now).ShouldBeFalse();
    }

    [Fact]
    public void Production_profile_cannot_load_test_only_recovery_key()
    {
        var key = new ArtifactVerificationKey(
            "test-key",
            ArtifactSignaturePurposes.OwnerRecoveryResponse,
            [1, 2, 3],
            DateTimeOffset.UnixEpoch,
            null,
            IsTestOnly: true);

        Should.Throw<InvalidOperationException>(() => new ArtifactPublicKeyRing([key], productionProfile: true));
    }
}

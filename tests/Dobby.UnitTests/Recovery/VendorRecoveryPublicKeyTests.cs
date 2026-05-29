using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class VendorRecoveryPublicKeyTests
{
    [Fact]
    public void Production_vendor_recovery_public_key_fails_closed_until_key_material_is_embedded()
    {
        IVendorRecoveryPublicKey publicKey = new VendorRecoveryPublicKey();

        publicKey.Verify([1, 2, 3], [4, 5, 6]).ShouldBeFalse();
    }
}

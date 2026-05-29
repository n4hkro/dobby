using Dobby.Infrastructure.HardwareIdentity;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class WindowsHardwareIdentityProviderTests
{
    [Fact]
    public void Provider_is_constructible_for_windows_adapter_slice()
    {
        var provider = new WindowsHardwareIdentityProvider();

        provider.ShouldBeAssignableTo<IHardwareIdentityProvider>();
    }
}

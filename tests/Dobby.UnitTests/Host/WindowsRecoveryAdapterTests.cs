using Dobby.Host.Login;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class WindowsRecoveryAdapterTests
{
    [Fact]
    public void Windows_recovery_adapters_provide_concrete_host_owned_implementations()
    {
        new WindowsRecoveryFilePicker().ShouldBeAssignableTo<IRecoveryFilePicker>();
        new WindowsRecoveryExportFolderOpener().ShouldBeAssignableTo<IRecoveryExportFolderOpener>();
        new WindowsRecoveryExportPathClipboard().ShouldBeAssignableTo<IRecoveryExportPathClipboard>();
    }
}

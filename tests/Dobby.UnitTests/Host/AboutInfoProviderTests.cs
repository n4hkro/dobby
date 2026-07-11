using Dobby.Core.Versioning;
using Dobby.Host.Settings;
using Dobby.Localization;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class AboutInfoProviderTests
{
    [Fact]
    public void About_info_uses_host_version_provider_and_support_safe_device_code()
    {
        var provider = new AboutInfoProvider(
            new FixedHostVersionProvider(new HostVersion(0, 1, 0)),
            new FixedSupportDeviceCodeProvider("device-redacted-1234"),
            "support@example.invalid",
            "Copyright Dobby");

        var info = provider.GetInfo(isOwner: true, ownerIdentity: "owner");

        info.AppVersion.ShouldBe("0.1.0");
        info.DeviceCode.ShouldBe("device-redacted-1234");
        info.ShowsLicenseStatus.ShouldBeFalse();
    }

    [Fact]
    public void Managed_user_gets_neutral_owner_identity_resource_key()
    {
        var provider = new AboutInfoProvider(
            new FixedHostVersionProvider(new HostVersion(0, 1, 0)),
            new FixedSupportDeviceCodeProvider("device-redacted-1234"),
            "support@example.invalid",
            "Copyright Dobby");

        var info = provider.GetInfo(isOwner: false, ownerIdentity: "owner");

        info.OwnerIdentityText.ShouldBe(HostResourceKeys.AboutOwnerStatusManaged);
        info.OwnerIdentityText.ShouldNotBe("owner");
    }

    [Fact]
    public void About_info_does_not_expose_raw_sensitive_material()
    {
        var provider = new AboutInfoProvider(
            new FixedHostVersionProvider(new HostVersion(0, 1, 0)),
            new FixedSupportDeviceCodeProvider("device-redacted-1234"),
            "support@example.invalid",
            "Copyright Dobby");

        var info = provider.GetInfo(isOwner: true, ownerIdentity: "owner");
        var flattened = string.Join("|", [
            info.DeviceCode,
            info.OwnerIdentityText,
            info.SupportContact,
            info.LegalLine
        ]);

        flattened.ShouldNotContain("password");
        flattened.ShouldNotContain("session");
        flattened.ShouldNotContain("license-payload");
        flattened.ShouldNotContain("private-key");
    }
}

using Dobby.Core.Versioning;

namespace Dobby.Host.Settings;

public sealed class AboutInfoProvider(
    IHostVersionProvider versionProvider,
    ISupportDeviceCodeProvider deviceCodeProvider,
    string supportContact,
    string legalLine,
    IHostBuildMetadataProvider? buildMetadataProvider = null)
{
    public AboutInfo GetInfo(bool isOwner, string? ownerIdentity)
    {
        var version = versionProvider.Current.ToString();
        var buildMetadata = buildMetadataProvider?.Current ?? new HostBuildMetadata(version, "metadata-unavailable");
        return new AboutInfo(
            version,
            buildMetadata.BuildNumber,
            buildMetadata.BuildDate,
            "local",
            "skeleton",
            deviceCodeProvider.CurrentRedactedCode(),
            [],
            supportContact,
            legalLine,
            isOwner && !string.IsNullOrWhiteSpace(ownerIdentity)
                ? ownerIdentity
                : "About.OwnerStatus.Registered",
            ShowsLicenseStatus: false);
    }
}

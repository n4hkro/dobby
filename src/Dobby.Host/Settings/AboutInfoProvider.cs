using Dobby.Core.Versioning;
using Dobby.Configuration;
using Dobby.Localization;

namespace Dobby.Host.Settings;

public sealed class AboutInfoProvider(
    IHostVersionProvider versionProvider,
    ISupportDeviceCodeProvider deviceCodeProvider,
    ProductOptions productOptions,
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
            productOptions.ArtifactMode == ProductArtifactMode.PortableSupport
                ? HostResourceKeys.AboutArtifactModePortableSupport
                : HostResourceKeys.AboutArtifactModeInstalled,
            deviceCodeProvider.CurrentRedactedCode(),
            [],
            productOptions.SupportContact,
            productOptions.LegalLine,
            isOwner && !string.IsNullOrWhiteSpace(ownerIdentity)
                ? ownerIdentity
                : HostResourceKeys.AboutOwnerStatusManaged,
            ShowsLicenseStatus: false);
    }
}

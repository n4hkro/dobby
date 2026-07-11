namespace Dobby.Host.Settings;

public sealed class AboutSettingsModel(AboutInfo info)
{
    public bool ShowsLicenseStatus => info.ShowsLicenseStatus;

    public bool HasLicenseImportAction => false;

    public AboutInfo Info => info;

    public AboutSettingsSection BuildSection()
    {
        return new AboutSettingsSection(
            [
                Dobby.Localization.HostResourceKeys.AboutAppVersion,
                Dobby.Localization.HostResourceKeys.AboutBuildNumber,
                Dobby.Localization.HostResourceKeys.AboutBuildDate,
                Dobby.Localization.HostResourceKeys.AboutHostEdition,
                Dobby.Localization.HostResourceKeys.AboutHostChannel,
                Dobby.Localization.HostResourceKeys.AboutDeviceCode,
                Dobby.Localization.HostResourceKeys.AboutPlugins,
                Dobby.Localization.HostResourceKeys.AboutSupportContact,
                Dobby.Localization.HostResourceKeys.AboutLegalLine,
                Dobby.Localization.HostResourceKeys.AboutOwnerIdentity
            ],
            ShowsLicenseStatus,
            HasLicenseImportAction);
    }
}

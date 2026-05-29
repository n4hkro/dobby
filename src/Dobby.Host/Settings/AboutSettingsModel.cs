namespace Dobby.Host.Settings;

public sealed class AboutSettingsModel(AboutInfo info)
{
    public bool ShowsLicenseStatus => info.ShowsLicenseStatus;

    public bool HasLicenseImportAction => false;

    public AboutInfo Info => info;
}

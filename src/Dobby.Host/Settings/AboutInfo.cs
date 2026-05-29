namespace Dobby.Host.Settings;

public sealed record AboutInfo(
    string AppVersion,
    string BuildNumber,
    string BuildDate,
    string HostEdition,
    string HostChannel,
    string DeviceCode,
    IReadOnlyList<PluginAboutInfo> Plugins,
    string SupportContact,
    string LegalLine,
    string OwnerIdentityText,
    bool ShowsLicenseStatus);

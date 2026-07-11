using Dobby.Localization;
using Shouldly;
using System.Xml.Linq;

namespace Dobby.UnitTests.Localization;

public sealed class SettingsLocalizationStringCatalogTests
{
    private static readonly string[] RequiredSettingsAndAboutKeys =
    {
        HostResourceKeys.SettingsAppearance,
        HostResourceKeys.SettingsAccount,
        HostResourceKeys.SettingsSecuritySession,
        HostResourceKeys.SettingsUserManagement,
        HostResourceKeys.SettingsAbout,
        HostResourceKeys.ShellProfileAccountInfo,
        HostResourceKeys.ShellProfileChangePassword,
        HostResourceKeys.ShellWorkSessionRestore,
        HostResourceKeys.ShellProfilePasswordChangeBusy,
        HostResourceKeys.ShellProfilePasswordChangeDenied,
        HostResourceKeys.ShellProfilePasswordChangeError,
        HostResourceKeys.ShellProfilePasswordChangeSuccess,
        HostResourceKeys.ShellProfileManualLockBusy,
        HostResourceKeys.ShellProfileManualLockDenied,
        HostResourceKeys.ShellProfileManualLockError,
        HostResourceKeys.ShellProfileManualLockSuccess,
        HostResourceKeys.ShellDiagnosticsExportBusy,
        HostResourceKeys.ShellDiagnosticsExportDenied,
        HostResourceKeys.ShellDiagnosticsExportError,
        HostResourceKeys.ShellDiagnosticsExportSuccess,
        HostResourceKeys.ShellProfileLogoutBusy,
        HostResourceKeys.ShellProfileLogoutDenied,
        HostResourceKeys.ShellProfileLogoutError,
        HostResourceKeys.ShellProfileLogoutSuccess,
        HostResourceKeys.UserManagementPermissionDenied,
        HostResourceKeys.AboutAppVersion,
        HostResourceKeys.AboutBuildNumber,
        HostResourceKeys.AboutBuildDate,
        HostResourceKeys.AboutHostEdition,
        HostResourceKeys.AboutHostChannel,
        HostResourceKeys.AboutDeviceCode,
        HostResourceKeys.AboutPlugins,
        HostResourceKeys.AboutPluginsEmpty,
        HostResourceKeys.AboutSupportContact,
        HostResourceKeys.AboutLegalLine,
        HostResourceKeys.AboutOwnerIdentity,
        HostResourceKeys.AboutOwnerStatusManaged,
        HostResourceKeys.AboutOwnerStatusRegistered,
        "Dobby.Host.Settings.Appearance.Theme",
        "Dobby.Host.Settings.Appearance.Theme.Light",
        "Dobby.Host.Settings.Appearance.Theme.Dark",
        "Dobby.Host.Settings.SecuritySession.AutoLockEnabled",
        "Dobby.Host.Settings.SecuritySession.AutoLockMinutes",
        "Dobby.Host.UserManagement.CreateManagedUser",
        "Dobby.Host.UserManagement.UpdateDisplayName",
        "Dobby.Host.UserManagement.UpdateActiveState",
        "Dobby.Host.UserManagement.ResetPassword",
        "Dobby.Host.UserManagement.GrantPermission",
        "Dobby.Host.UserManagement.RevokePermission"
    };

    public static TheoryData<string> SettingsKeys => new(RequiredSettingsAndAboutKeys);

    [Theory]
    [MemberData(nameof(SettingsKeys))]
    public void Catalog_contains_sprint6_settings_about_and_user_management_keys(string key)
    {
        var value = new HostStringCatalog().GetString(key);

        value.ShouldNotBe($"[[{key}]]");
        value.ShouldNotContain("permission-denied");
        value.ShouldNotContain("Forbidden");
    }

    [Fact]
    public void Settings_about_user_management_keys_have_resx_parity()
    {
        var resourceKeyValues = typeof(HostResourceKeys)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var key in RequiredSettingsAndAboutKeys)
        {
            resourceKeyValues.ShouldContain(key);
        }

        var azAz = ReadResxKeys("Host.az-AZ.resx");
        var enUs = ReadResxKeys("Host.en-US.resx");
        var trTr = ReadResxKeys("Host.tr-TR.resx");

        foreach (var key in RequiredSettingsAndAboutKeys)
        {
            azAz.ShouldContain(key);
            enUs.ShouldContain(key);
            trTr.ShouldContain(key);
        }
        enUs.ShouldBe(azAz);
        trTr.ShouldBe(azAz);
    }

    [Fact]
    public void English_and_turkish_visible_strings_do_not_reuse_azerbaijani_values()
    {
        var azAz = ReadResxValues("Host.az-AZ.resx");
        var enUs = ReadResxValues("Host.en-US.resx");
        var trTr = ReadResxValues("Host.tr-TR.resx");
        var visibleLocalizedKeys = new[]
        {
            HostResourceKeys.SettingsAppearance,
            HostResourceKeys.SettingsAccount,
            HostResourceKeys.SettingsSecuritySession,
            HostResourceKeys.SettingsUserManagement,
            HostResourceKeys.UserManagementPermissionDenied,
            HostResourceKeys.ShellProfileChangePassword,
            HostResourceKeys.ShellDiagnosticsExported,
            HostResourceKeys.ShellWorkSessionRestore,
            HostResourceKeys.ShellProfilePasswordChangeBusy,
            HostResourceKeys.ShellProfileManualLockDenied,
            HostResourceKeys.ShellDiagnosticsExportError,
            HostResourceKeys.ShellProfileLogoutSuccess
        };

        foreach (var key in visibleLocalizedKeys)
        {
            enUs[key].ShouldNotBe(azAz[key], $"en-US must not reuse the az-AZ value for '{key}'.");
            trTr[key].ShouldNotBe(azAz[key], $"tr-TR must not reuse the az-AZ value for '{key}'.");
        }
    }

    private static HashSet<string> ReadResxKeys(string fileName)
    {
        var path = Path.Combine(RepoRoot().FullName, "src", "Dobby.Localization", "Resources", fileName);
        File.Exists(path).ShouldBeTrue($"Expected localization resource file '{fileName}'.");
        return XDocument.Load(path)
            .Descendants("data")
            .Select(element => element.Attribute("name")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal)!;
    }

    private static IReadOnlyDictionary<string, string> ReadResxValues(string fileName)
    {
        var path = Path.Combine(RepoRoot().FullName, "src", "Dobby.Localization", "Resources", fileName);
        File.Exists(path).ShouldBeTrue($"Expected localization resource file '{fileName}'.");
        return XDocument.Load(path)
            .Descendants("data")
            .ToDictionary(
                element => element.Attribute("name")?.Value ?? string.Empty,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}

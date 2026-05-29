using Dobby.Localization;
using Shouldly;

namespace Dobby.UnitTests.Localization;

public sealed class SettingsLocalizationStringCatalogTests
{
    public static TheoryData<string> SettingsKeys => new()
    {
        HostResourceKeys.SettingsAppearance,
        HostResourceKeys.SettingsAccount,
        HostResourceKeys.SettingsSecuritySession,
        HostResourceKeys.SettingsUserManagement,
        HostResourceKeys.SettingsAbout,
        HostResourceKeys.UserManagementPermissionDenied,
        HostResourceKeys.AboutOwnerStatusRegistered
    };

    [Theory]
    [MemberData(nameof(SettingsKeys))]
    public void Catalog_contains_sprint6_settings_about_and_user_management_keys(string key)
    {
        var value = new HostStringCatalog().GetString(key);

        value.ShouldNotBe($"[[{key}]]");
        value.ShouldNotContain("permission-denied");
        value.ShouldNotContain("Forbidden");
    }
}

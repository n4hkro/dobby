using Dobby.Configuration;
using Dobby.Core;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class SettingsPreferenceSurfaceTests
{
    [Fact]
    public void Defaults_keep_auto_lock_passive_and_theme_light()
    {
        var preferences = SettingsPreferences.Default;

        preferences.Appearance.Theme.ShouldBe(AppThemePreference.Light);
        preferences.SecuritySession.AutoLockEnabled.ShouldBeFalse();
        preferences.SecuritySession.AutoLockMinutes.ShouldBe(30);
        preferences.ShellView.SidebarCollapsed.ShouldBeFalse();
        preferences.ShellView.SidebarOpenNodeIds.ShouldBeEmpty();
        preferences.ShellView.WindowWidth.ShouldBe(1024);
        preferences.ShellView.WindowHeight.ShouldBe(680);
        preferences.ShellView.RestoreLastActiveModuleOnStartup.ShouldBeFalse();
    }

    [Fact]
    public void Security_session_preferences_validate_minute_input()
    {
        SecuritySessionPreference.Create(autoLockEnabled: true, autoLockMinutes: 5)
            .Value.AutoLockMinutes.ShouldBe(5);

        SecuritySessionPreference.Create(autoLockEnabled: true, autoLockMinutes: 0)
            .ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Fact]
    public void Preference_source_contract_is_owned_by_configuration_without_infrastructure_reference()
    {
        typeof(ISettingsPreferenceSource).Namespace.ShouldBe("Dobby.Configuration");
        typeof(ISettingsPreferenceSource).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ShouldNotContain("Dobby.Infrastructure");
    }

    [Fact]
    public void Authenticated_user_scope_uses_stable_non_pii_key_namespace()
    {
        var first = SettingsPreferenceScope.ForAuthenticatedUser("owner@example.local");
        var second = SettingsPreferenceScope.ForAuthenticatedUser("owner@example.local");
        var other = SettingsPreferenceScope.ForAuthenticatedUser("managed@example.local");

        first.KeyPrefix.ShouldBe(second.KeyPrefix);
        first.KeyPrefix.ShouldStartWith("settings.user.");
        first.KeyPrefix.ShouldNotBe(other.KeyPrefix);
        first.KeyPrefix.ShouldNotContain("owner", Case.Insensitive);
        first.KeyPrefix.ShouldNotContain("@", Case.Sensitive);
        first.KeyPrefix.ShouldNotContain("example", Case.Insensitive);
    }
}

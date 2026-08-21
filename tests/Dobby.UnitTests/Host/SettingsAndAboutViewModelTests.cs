using Dobby.Configuration;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Host.Settings;
using Dobby.Localization;
using Dobby.Security.Authorization;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class SettingsAndAboutViewModelTests
{
    [Fact]
    public void Navigation_contains_minimum_sprint6_categories_and_gates_user_management()
    {
        var source = new MemoryPreferenceSource();
        var ownerNavigation = new SettingsNavigationModel(AllowingPolicy(), source);
        var managedNavigation = new SettingsNavigationModel(DenyingPolicy(), source);

        ownerNavigation.Build("owner", CorrelationId.New()).Select(category => category.Key)
            .ShouldBe(["appearance", "account", "security-session", "user-management", "about"]);
        ownerNavigation.Build("owner", CorrelationId.New()).Single(category => category.Key == "user-management")
            .IsAvailable.ShouldBeTrue();
        managedNavigation.Build("managed", CorrelationId.New()).Single(category => category.Key == "user-management")
            .IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void Appearance_model_reads_and_writes_shared_configuration_preference_source()
    {
        var source = new MemoryPreferenceSource();
        var first = new AppearanceSettingsModel(source);
        var second = new AppearanceSettingsModel(source);

        first.SetTheme(AppThemePreference.Dark).IsSuccess.ShouldBeTrue();

        second.CurrentTheme.ShouldBe(AppThemePreference.Dark);
    }

    [Fact]
    public void Account_model_exposes_password_shortcut_without_lock_or_logout_duplicates()
    {
        var passwordChangeCalls = 0;
        var correlationId = CorrelationId.New();
        var account = new AccountSettingsModel(
            (userId, sessionId, currentPassword, newPassword, confirmation, rememberMe, actualCorrelationId) =>
            {
                userId.ShouldBe("user-1");
                sessionId.ShouldBe("session-1");
                currentPassword.ShouldBe("old password");
                newPassword.ShouldBe("new password");
                confirmation.ShouldBe("new password");
                rememberMe.ShouldBeTrue();
                actualCorrelationId.ShouldBe(correlationId);
                passwordChangeCalls++;
                return Result.Success();
            });

        account.HasPasswordChangeShortcut.ShouldBeTrue();
        account.HasManualLockShortcut.ShouldBeFalse();
        account.HasLogoutShortcut.ShouldBeFalse();

        account.ChangePassword(
                "user-1",
                "session-1",
                "old password",
                "new password",
                "new password",
                rememberMe: true,
                correlationId)
            .IsSuccess.ShouldBeTrue();
        passwordChangeCalls.ShouldBe(1);
        var source = File.ReadAllText(Path.Combine(RepoRoot().FullName, "src", "Dobby.Host", "Settings", "AccountSettingsModel.cs"));
        source.ShouldNotContain(string.Concat("Forget", "Saved", "Login"));
        source.ShouldNotContain(string.Concat("Saved", "Login", "Clearer"));
    }
    [Fact]
    public void Security_session_model_defaults_passive_and_accepts_minute_input()
    {
        var model = new SecuritySessionSettingsModel(new MemoryPreferenceSource());

        model.AutoLockEnabled.ShouldBeFalse();
        model.AutoLockMinutes.ShouldBe(30);

        model.SetAutoLock(autoLockEnabled: true, autoLockMinutes: 12).IsSuccess.ShouldBeTrue();
        model.AutoLockEnabled.ShouldBeTrue();
        model.AutoLockMinutes.ShouldBe(12);
    }

    [Fact]
    public void Settings_source_does_not_add_license_import_or_status_to_about()
    {
        var about = new AboutSettingsModel(new AboutInfo(
            "0.1.0",
            "0.1.0",
            "unknown",
            "local",
            "skeleton",
            "device-1234",
            [],
            "support@example.invalid",
            "Copyright Dobby",
            "About.OwnerStatus.Registered",
            ShowsLicenseStatus: false));

        about.ShowsLicenseStatus.ShouldBeFalse();
        about.HasLicenseImportAction.ShouldBeFalse();
    }

    [Fact]
    public void Settings_source_exposes_required_rendered_sections_and_user_management_actions()
    {
        var appearance = new AppearanceSettingsModel(new MemoryPreferenceSource());
        var account = new AccountSettingsModel((_, _, _, _, _, _, _) => Result.Success());
        var security = new SecuritySessionSettingsModel(new MemoryPreferenceSource());
        var about = new AboutSettingsModel(new AboutInfo(
            "0.1.0",
            "100",
            "2026-07-08",
            "local",
            "stable",
            "device-1234",
            [],
            "support@example.invalid",
            "Copyright Dobby",
            HostResourceKeys.AboutOwnerStatusRegistered,
            ShowsLicenseStatus: false));

        var appearanceSection = InvokeParameterlessMember(appearance, "BuildSection");
        ReadStringProperty(appearanceSection, "ThemeLabelKey").ShouldBe("Dobby.Host.Settings.Appearance.Theme");
        ReadSequenceProperty(appearanceSection, "ThemeOptionLabelKeys")
            .ShouldBe(["Dobby.Host.Settings.Appearance.Theme.Light", "Dobby.Host.Settings.Appearance.Theme.Dark"]);

        var accountSection = InvokeParameterlessMember(account, "BuildSection");
        ReadStringProperty(accountSection, "AccountInfoActionLabelKey").ShouldBe(HostResourceKeys.ShellProfileAccountInfo);
        ReadStringProperty(accountSection, "ChangePasswordActionLabelKey").ShouldBe(HostResourceKeys.ShellProfileChangePassword);

        var securitySection = InvokeParameterlessMember(security, "BuildSection");
        ReadStringProperty(securitySection, "AutoLockEnabledLabelKey").ShouldBe("Dobby.Host.Settings.SecuritySession.AutoLockEnabled");
        ReadStringProperty(securitySection, "AutoLockMinutesLabelKey").ShouldBe("Dobby.Host.Settings.SecuritySession.AutoLockMinutes");
        ReadBooleanProperty(securitySection, "AutoLockEnabled").ShouldBeFalse();
        ReadIntProperty(securitySection, "AutoLockMinutes").ShouldBe(30);

        var userManagementModelType = typeof(SettingsNavigationModel).Assembly.GetType("Dobby.Host.Settings.UserManagementSettingsModel");
        userManagementModelType.ShouldNotBeNull();
        var userManagementModel = Activator.CreateInstance(userManagementModelType!, args: [false]);
        userManagementModel.ShouldNotBeNull();
        var userManagementSection = InvokeParameterlessMember(userManagementModel!, "BuildSection");
        ReadBooleanProperty(userManagementSection, "IsAvailable").ShouldBeFalse();
        ReadStringProperty(userManagementSection, "PermissionDeniedLabelKey").ShouldBe(HostResourceKeys.UserManagementPermissionDenied);
        ReadSequenceProperty(userManagementSection, "ActionLabelKeys").ShouldBe(
            [
                "Dobby.Host.UserManagement.CreateManagedUser",
                "Dobby.Host.UserManagement.UpdateDisplayName",
                "Dobby.Host.UserManagement.UpdateActiveState",
                "Dobby.Host.UserManagement.ResetPassword",
                "Dobby.Host.UserManagement.GrantPermission",
                "Dobby.Host.UserManagement.RevokePermission"
            ]);

        var aboutSection = InvokeParameterlessMember(about, "BuildSection");
        ReadSequenceProperty(aboutSection, "FieldLabelKeys").ShouldBe(
            [
                HostResourceKeys.AboutAppVersion,
                HostResourceKeys.AboutBuildNumber,
                HostResourceKeys.AboutBuildDate,
                HostResourceKeys.AboutHostEdition,
                HostResourceKeys.AboutHostChannel,
                HostResourceKeys.AboutDeviceCode,
                HostResourceKeys.AboutPlugins,
                HostResourceKeys.AboutSupportContact,
                HostResourceKeys.AboutLegalLine,
                HostResourceKeys.AboutOwnerIdentity
            ]);
    }

    [Fact]
    public void Live_shell_settings_state_renders_security_values_and_non_disclosing_user_management_gate()
    {
        var shell = Dobby.Shell.ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        shell.SetSettingsAndAbout(
            [
                new Dobby.Shell.Layout.ShellSettingsCategoryItem("appearance", "Appearance", true),
                new Dobby.Shell.Layout.ShellSettingsCategoryItem("account", "Account", true),
                new Dobby.Shell.Layout.ShellSettingsCategoryItem("security-session", "Security / Session", true),
                new Dobby.Shell.Layout.ShellSettingsCategoryItem("user-management", "User Management", false),
                new Dobby.Shell.Layout.ShellSettingsCategoryItem("about", "About", true)
            ],
            [
                new Dobby.Shell.Layout.ShellAboutField("app-version", "App version", "1.0.0")
            ],
            autoLockEnabled: true,
            autoLockMinutes: 12);

        shell.AppearanceThemeOptions.Select(option => option.Theme).ShouldBe(["Light", "Dark"]);
        shell.SecurityAutoLockEnabled.ShouldBeTrue();
        shell.SecurityAutoLockMinutes.ShouldBe(12);
        shell.UserManagementIsAvailable.ShouldBeFalse();
        shell.UserManagementSurfaceVisibility.ShouldBe(System.Windows.Visibility.Collapsed);
        shell.UserManagementActionsVisibility.ShouldBe(System.Windows.Visibility.Collapsed);
        shell.UserManagementActionItems.ShouldBeEmpty();
        shell.SettingsCategories.Select(category => category.Key)
            .ShouldBe(["appearance", "account", "security-session", "about"]);
    }

    private static ICapabilityPolicy<OperationPermissionCapability> AllowingPolicy()
    {
        return new ConstantPolicy(true);
    }

    private static ICapabilityPolicy<OperationPermissionCapability> DenyingPolicy()
    {
        return new ConstantPolicy(false);
    }

    private sealed class ConstantPolicy(bool allowed) : ICapabilityPolicy<OperationPermissionCapability>
    {
        public Result<PolicyDecision> Evaluate(OperationPermissionCapability capability, PolicyContext context)
        {
            return Result.Success(allowed
                ? PolicyDecision.Allow()
                : PolicyDecision.Deny(OperationAuthorizationPolicy.NonDisclosingDenyReason));
        }
    }

    private sealed class MemoryPreferenceSource : ISettingsPreferenceSource
    {
        private SettingsPreferences preferences = SettingsPreferences.Default;

        public Result<SettingsPreferences> Load()
        {
            return Result.Success(preferences);
        }

        public Result Save(SettingsPreferences preferences)
        {
            this.preferences = preferences;
            return Result.Success();
        }
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

    private static object InvokeParameterlessMember(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName);
        method.ShouldNotBeNull();
        return method!.Invoke(target, null)!;
    }

    private static bool ReadBooleanProperty(object target, string propertyName)
    {
        return (bool)ReadProperty(target, propertyName);
    }

    private static int ReadIntProperty(object target, string propertyName)
    {
        return (int)ReadProperty(target, propertyName);
    }

    private static string ReadStringProperty(object target, string propertyName)
    {
        return (string)ReadProperty(target, propertyName);
    }

    private static IReadOnlyList<string> ReadSequenceProperty(object target, string propertyName)
    {
        return ((IEnumerable<object?>)ReadProperty(target, propertyName))
            .Select(value => value?.ToString() ?? string.Empty)
            .ToArray();
    }

    private static object ReadProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName);
        property.ShouldNotBeNull();
        return property!.GetValue(target)!;
    }
}

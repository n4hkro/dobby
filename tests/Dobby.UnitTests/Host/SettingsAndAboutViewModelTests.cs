using Dobby.Configuration;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Host.Settings;
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
            (userId, sessionId, newPassword, rememberMe, actualCorrelationId) =>
            {
                userId.ShouldBe("user-1");
                sessionId.ShouldBe("session-1");
                newPassword.ShouldBe("new password");
                rememberMe.ShouldBeTrue();
                actualCorrelationId.ShouldBe(correlationId);
                passwordChangeCalls++;
                return Result.Success();
            });

        account.HasPasswordChangeShortcut.ShouldBeTrue();
        account.HasManualLockShortcut.ShouldBeFalse();
        account.HasLogoutShortcut.ShouldBeFalse();

        account.ChangePassword("user-1", "session-1", "new password", rememberMe: true, correlationId)
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
}
